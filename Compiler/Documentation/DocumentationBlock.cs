using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Represents a source-backed documentation block and its optional declaration attachment.
/// </summary>
public sealed class DocumentationBlock
{
    /// <summary>
    /// Gets the source snapshot that owns the block and all of its line spans.
    /// </summary>
    public SourceSnapshot Snapshot => this.Span.Snapshot;

    /// <summary>
    /// Gets the complete block span from the first documentation introducer through the last comment character.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// Gets the documentation-line comment spans in source order, including each <c>///</c> introducer.
    /// </summary>
    public IReadOnlyList<SourceSpan> LineSpans { get; }

    /// <summary>
    /// Gets the parsed prose and directive metadata for this block.
    /// </summary>
    public DocumentationContent Content { get; }

    /// <summary>
    /// Gets the eligible syntax declaration attached to this block, or <see langword="null"/> when the block is detached.
    /// </summary>
    public SyntaxNode? Declaration { get; }

    /// <summary>
    /// Gets whether the block has an eligible declaration attachment.
    /// </summary>
    public bool IsAttached => this.Declaration is not null;

    internal DocumentationBlock(SourceSpan span, IReadOnlyList<SourceSpan> lineSpans, SyntaxNode? declaration, DocumentationContent content)
    {
        ArgumentNullException.ThrowIfNull(lineSpans);
        ArgumentNullException.ThrowIfNull(content);

        if (lineSpans.Count == 0)
        {
            throw new ArgumentException("A documentation block must contain at least one documentation line.", nameof(lineSpans));
        }

        SourceSnapshot snapshot = span.Snapshot;
        if (span.Start < 0 || span.End < span.Start || span.End > snapshot.SourceLength)
        {
            throw new ArgumentException("The documentation block span must be a valid range in its source snapshot.", nameof(span));
        }

        SourceSpan[] lines = [.. lineSpans];
        int previousEnd = -1;

        foreach (SourceSpan line in lines)
        {
            if (!ReferenceEquals(line.Snapshot, snapshot)
                || line.Start < 0
                || line.End < line.Start
                || line.End > snapshot.SourceLength
                || line.Start < previousEnd
                || line.Start < span.Start
                || line.End > span.End)
            {
                throw new ArgumentException("Documentation lines must be ordered within the block's source snapshot and span.", nameof(lineSpans));
            }

            previousEnd = line.End;
        }

        if (span.Start != lines[0].Start || span.End != lines[^1].End)
        {
            throw new ArgumentException("A documentation block span must cover exactly its first and last comment lines.", nameof(span));
        }

        if (declaration is not null && !ReferenceEquals(declaration.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("The attached declaration must belong to the documentation block's source snapshot.", nameof(declaration));
        }

        if (!ReferenceEquals(content.Prose.Snapshot, snapshot)
            || content.Prose.Lines.Any(line => line.Start < span.Start || line.End > span.End)
            || content.Directives.Any(directive => directive.Span.Start < span.Start || directive.Span.End > span.End
                || directive.Description.Lines.Any(line => line.Start < span.Start || line.End > span.End))
            || content.InlineReferences.Any(reference => reference.Span.Start < span.Start || reference.Span.End > span.End
                || (reference.TargetNameSpan is SourceSpan target && (target.Start < span.Start || target.End > span.End))))
        {
            throw new ArgumentException("Documentation content must belong to the block source span.", nameof(content));
        }

        this.Span = span;
        this.Content = content;
        this.LineSpans = Array.AsReadOnly(lines);
        this.Declaration = declaration;
    }
}