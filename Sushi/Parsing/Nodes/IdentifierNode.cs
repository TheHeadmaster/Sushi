using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class IdentifierNode(Token startToken) : SyntaxNode(startToken)
{
    public string Name { get; set; }
}
