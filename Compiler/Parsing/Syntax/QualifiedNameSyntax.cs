using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a dotted sequence of identifier components, including missing
/// components synthesized during recovery from malformed qualified names.
/// </summary>
public sealed class QualifiedNameSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.QualifiedName;

    /// <inheritdoc />
    public override SourceSpan Span
    {
        get
        {
            SourceSpan first = this.Segments[0].Span;
            int end = this.Segments[^1].Span.End;

            if (this.Separators.Count > 0)
            {
                end = Math.Max(end, this.Separators[^1].Span.End);
            }

            return new SourceSpan(first.Snapshot, first.Start, end);
        }
    }

    /// <summary>
    /// Gets the identifier components of the name, including synthetic missing components.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Segments { get; }

    /// <summary>
    /// Gets the source-backed dots separating adjacent components.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Separators { get; }

    /// <summary>
    /// Creates a qualified name from its identifier components and separating dots.
    /// </summary>
    /// <param name="segments">
    /// The components in source order, including any missing components.
    /// </param>
    /// <param name="separators">
    /// The dots separating neighboring components.
    /// </param>
    public QualifiedNameSyntax(IReadOnlyList<SyntaxToken> segments, IReadOnlyList<SyntaxToken> separators)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(separators);

        SyntaxToken[] segmentArray = [.. segments];
        SyntaxToken[] separatorArray = [.. separators];

        if (segmentArray.Length == 0)
        {
            throw new ArgumentException("A qualified name requires at least one segment.", nameof(segments));
        }

        if (separatorArray.Length != segmentArray.Length - 1)
        {
            throw new ArgumentException("A qualified name requires exactly one separator between each pair of segments.", nameof(separators));
        }

        if (segmentArray.Any(segment => segment.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken))
        {
            throw new ArgumentException("Qualified-name segments must be identifier syntax tokens.");
        }

        if (separatorArray.Any(separator => separator.Type != SyntaxType.DotToken))
        {
            throw new ArgumentException("Qualified-name separators must be dot syntax tokens.", nameof(separators));
        }

        SourceSnapshot snapshot = segmentArray[0].Span.Snapshot;

        if (segmentArray.Any(segment => !ReferenceEquals(segment.Span.Snapshot, snapshot)) || separatorArray.Any(separator => !ReferenceEquals(separator.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All qualified-name tokens must originate from the same source snapshot.");
        }

        this.Segments = segmentArray;
        this.Separators = separatorArray;
    }
}