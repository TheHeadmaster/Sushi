using System.Collections.ObjectModel;
using System.Text;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Identifies a documentation directive understood by the initial documentation contract.
/// </summary>
public enum DocumentationDirectiveKind
{
    Parameter,
    Return
}

/// <summary>Identifies an explicit semantic reference embedded in documentation Markdown.</summary>
public enum DocumentationReferenceKind
{
    Link,
    Parameter
}

/// <summary>Represents a completed inline reference with exact source-backed target locations.</summary>
public sealed class DocumentationReference
{
    private readonly ReadOnlyCollection<SourceSpan> parameterTypeSpans;
    private readonly SourceSpan? writtenTargetSpan;
    private readonly SourceSpan? targetQualifierSpan;
    private readonly SourceSpan? displayTextSpan;

    /// <summary>Gets the kind of semantic documentation reference.</summary>
    public DocumentationReferenceKind Kind { get; }

    /// <summary>Gets the complete source span of the reference construct.</summary>
    public SourceSpan Span { get; }

    /// <summary>Gets the final identifier component span used for navigation and Rename.</summary>
    public SourceSpan? TargetNameSpan { get; }

    /// <summary>Gets the source spelling of the target identifier from its authoritative span.</summary>
    public string? TargetNameSpelling => GetSourceText(this.TargetNameSpan);

    /// <summary>Gets the written link target from its authoritative span.</summary>
    public string? WrittenTarget => GetSourceText(this.writtenTargetSpan);

    /// <summary>Gets the optional qualifier preceding a link target name.</summary>
    public string? TargetQualifier => GetSourceText(this.targetQualifierSpan);

    /// <summary>Gets explicit parameter type spellings from their source spans.</summary>
    public IReadOnlyList<string> ParameterTypeSpellings
        => Array.AsReadOnly(this.parameterTypeSpans.Select(span => GetSourceText(span) ?? string.Empty).ToArray());

    /// <summary>Gets whether the link includes an explicit parameter type signature.</summary>
    public bool HasExplicitSignature { get; }

    /// <summary>Gets optional display text, which is not interpreted as another symbol reference.</summary>
    public string? DisplayText => GetSourceText(this.displayTextSpan);

    /// <summary>Gets whether the completed construct has a structurally valid target.</summary>
    public bool IsWellFormed { get; }

    internal DocumentationReference(DocumentationReferenceKind kind, SourceSpan span, SourceSpan? targetNameSpan,
        SourceSpan? writtenTargetSpan, SourceSpan? targetQualifierSpan, IReadOnlyList<SourceSpan> parameterTypeSpans,
        SourceSpan? displayTextSpan, bool hasExplicitSignature, bool isWellFormed)
    {
        ArgumentNullException.ThrowIfNull(parameterTypeSpans);
        SourceSpan[] typeSpans = [.. parameterTypeSpans];
        if (typeSpans.Any(typeSpan => !IsContained(span, typeSpan))
            || (targetNameSpan is SourceSpan targetSpan && !IsContained(span, targetSpan))
            || (writtenTargetSpan is SourceSpan writtenSpan && !IsContained(span, writtenSpan))
            || (targetQualifierSpan is SourceSpan qualifierSpan && !IsContained(span, qualifierSpan))
            || (displayTextSpan is SourceSpan displaySpan && !IsContained(span, displaySpan)))
        {
            throw new ArgumentException("Documentation reference components must belong to and fit within their source construct.");
        }

        this.Kind = kind;
        this.Span = span;
        this.TargetNameSpan = targetNameSpan;
        this.writtenTargetSpan = writtenTargetSpan;
        this.targetQualifierSpan = targetQualifierSpan;
        this.parameterTypeSpans = Array.AsReadOnly(typeSpans);
        this.HasExplicitSignature = hasExplicitSignature;
        this.displayTextSpan = displayTextSpan;
        this.IsWellFormed = isWellFormed;
    }

    private static bool IsContained(SourceSpan parent, SourceSpan child)
        => ReferenceEquals(parent.Snapshot, child.Snapshot) && child.Start >= parent.Start && child.End <= parent.End;

    private static string? GetSourceText(SourceSpan? span)
    {
        if (span is not SourceSpan value)
        {
            return null;
        }

        return Encoding.UTF8.GetString(value.Snapshot.Bytes.Span[value.Start..value.End]);
    }
}

/// <summary>
/// Represents documentation text as ordered source-backed line segments.
/// </summary>
public sealed class DocumentationText
{
    private readonly ReadOnlyCollection<SourceSpan> lines;

    /// <summary>
    /// Gets the authoritative source snapshot containing all text segments.
    /// </summary>
    public SourceSnapshot Snapshot { get; }

    /// <summary>
    /// Gets the source-backed content for each logical documentation line.
    /// Empty spans preserve intentional blank documentation lines.
    /// </summary>
    public IReadOnlyList<SourceSpan> Lines => this.lines;

    /// <summary>
    /// Gets the line content joined with normalized LF separators for presentation.
    /// The original spelling and line endings remain available from <see cref="Snapshot"/> and <see cref="Lines"/>.
    /// </summary>
    public string Text
    {
        get
        {
            StringBuilder builder = new();
            ReadOnlySpan<byte> source = this.Snapshot.Bytes.Span;

            for (int index = 0; index < this.lines.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('\n');
                }

                SourceSpan line = this.lines[index];
                builder.Append(Encoding.UTF8.GetString(source[line.Start..line.End]));
            }

            return builder.ToString();
        }
    }

    internal DocumentationText(SourceSnapshot snapshot, IReadOnlyList<SourceSpan> lines)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(lines);

        SourceSpan[] copiedLines = [.. lines];
        int previousStart = -1;
        foreach (SourceSpan line in copiedLines)
        {
            if (!ReferenceEquals(line.Snapshot, snapshot) || line.Start < previousStart || line.End < line.Start || line.End > snapshot.SourceLength)
            {
                throw new ArgumentException("Documentation text lines must be ordered spans in the same source snapshot.", nameof(lines));
            }

            previousStart = line.Start;
        }

        this.Snapshot = snapshot;
        this.lines = Array.AsReadOnly(copiedLines);
    }
}

/// <summary>
/// Represents a recognized parameter or return directive with exact source locations.
/// </summary>
public sealed class DocumentationDirective
{
    /// <summary>
    /// Gets the recognized directive kind.
    /// </summary>
    public DocumentationDirectiveKind Kind { get; }

    /// <summary>
    /// Gets the canonical directive name without its introducer.
    /// </summary>
    public string Name => this.Kind is DocumentationDirectiveKind.Parameter ? "param" : "return";

    /// <summary>
    /// Gets the source span of the directive name, including its at-sign.
    /// </summary>
    public SourceSpan NameSpan { get; }

    /// <summary>
    /// Gets the parameter name and its source span for a parameter directive.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Gets the parameter-name span for a parameter directive.
    /// </summary>
    public SourceSpan? ParameterNameSpan { get; }

    /// <summary>
    /// Gets the complete source span covered by the directive and its continuation lines.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// Gets the source-backed description, including multiline formatting and blank lines.
    /// </summary>
    public DocumentationText Description { get; }

    internal DocumentationDirective(DocumentationDirectiveKind kind, SourceSpan nameSpan, string? parameterName, SourceSpan? parameterNameSpan, SourceSpan span, DocumentationText description)
    {
        if (!ReferenceEquals(nameSpan.Snapshot, span.Snapshot)
            || (parameterNameSpan is SourceSpan parameterSpan && !ReferenceEquals(parameterSpan.Snapshot, span.Snapshot))
            || !ReferenceEquals(description.Snapshot, span.Snapshot))
        {
            throw new ArgumentException("A documentation directive must use one authoritative source snapshot.");
        }

        if (nameSpan.Start < span.Start || nameSpan.End > span.End
            || (parameterNameSpan is SourceSpan name && (name.Start < span.Start || name.End > span.End)))
        {
            throw new ArgumentException("Directive component spans must be within the complete directive span.");
        }

        this.Kind = kind;
        this.NameSpan = nameSpan;
        this.ParameterName = parameterName;
        this.ParameterNameSpan = parameterNameSpan;
        this.Span = span;
        this.Description = description;
    }
}

/// <summary>
/// Holds parsed prose, directives, and explicit semantic references for one documentation block.
/// </summary>
public sealed class DocumentationContent
{
    private readonly ReadOnlyCollection<DocumentationDirective> directives;
    private readonly ReadOnlyCollection<DocumentationReference> inlineReferences;

    /// <summary>Gets prose lines that are not consumed as recognized directive descriptions.</summary>
    public DocumentationText Prose { get; }

    /// <summary>Gets recognized directives in source order.</summary>
    public IReadOnlyList<DocumentationDirective> Directives => this.directives;

    /// <summary>Gets completed inline semantic references in source order.</summary>
    public IReadOnlyList<DocumentationReference> InlineReferences => this.inlineReferences;

    internal DocumentationContent(DocumentationText prose, IReadOnlyList<DocumentationDirective> directives,
        IReadOnlyList<DocumentationReference> inlineReferences)
    {
        ArgumentNullException.ThrowIfNull(prose);
        ArgumentNullException.ThrowIfNull(directives);
        ArgumentNullException.ThrowIfNull(inlineReferences);

        int previousStart = -1;
        foreach (DocumentationDirective directive in directives)
        {
            if (!ReferenceEquals(directive.Span.Snapshot, prose.Snapshot) || directive.Span.Start < previousStart)
            {
                throw new ArgumentException("Documentation directives must be source ordered in one snapshot.", nameof(directives));
            }

            previousStart = directive.Span.Start;
        }

        DocumentationReference[] copiedReferences = [.. inlineReferences];
        previousStart = -1;
        foreach (DocumentationReference reference in copiedReferences)
        {
            if (!ReferenceEquals(reference.Span.Snapshot, prose.Snapshot) || reference.Span.Start < previousStart)
            {
                throw new ArgumentException("Inline references must be source ordered in one snapshot.", nameof(inlineReferences));
            }

            previousStart = reference.Span.Start;
        }

        this.Prose = prose;
        this.directives = Array.AsReadOnly([.. directives]);
        this.inlineReferences = Array.AsReadOnly(copiedReferences);
    }
}