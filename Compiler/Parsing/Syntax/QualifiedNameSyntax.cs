using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a dotted sequence of identifier components.
/// </summary>
public sealed class QualifiedNameSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.QualifiedName;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Segments[0].Span.Snapshot, this.Segments[0].Span.Start, this.Segments[^1].Span.End);

    /// <summary>
    /// Gets the identifier components of the name.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Segments { get; }

    /// <summary>
    /// Gets the dot tokens separating adjacent components.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Separators { get; }

    /// <summary>
    /// Creates a qualified name from its identifier components and separating dot tokens.
    /// </summary>
    /// <param name="segments">
    /// The identifier components of the qualified name.
    /// </param>
    /// <param name="separators">
    /// The dot tokens separating adjacent components.
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