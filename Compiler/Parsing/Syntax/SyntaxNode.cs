using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a structured source construct in a Sushi concrete syntax tree.
/// </summary>
public abstract class SyntaxNode
{
    /// <summary>
    /// Gets the grammatical type of this syntax node.
    /// </summary>
    public abstract SyntaxType Type { get; }

    /// <summary>
    /// Gets the source span covered by this syntax node.
    /// </summary>
    public abstract SourceSpan Span { get; }
}