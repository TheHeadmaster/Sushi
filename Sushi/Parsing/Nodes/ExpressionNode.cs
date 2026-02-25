using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class ExpressionNode(Token startToken) : SyntaxNode(startToken)
{
}
