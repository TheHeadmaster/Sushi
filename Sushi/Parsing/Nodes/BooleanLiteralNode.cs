using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a boolean literal.
/// </summary>
public sealed class BooleanLiteralNode(Token startToken) : SyntaxNode(startToken)
{
    public string Value { get; set; } = startToken.Value;
}
