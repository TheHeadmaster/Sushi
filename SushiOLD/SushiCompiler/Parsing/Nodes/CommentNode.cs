
using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.Nodes;

internal class CommentNode(Token comment) : SyntaxNode
{
    internal TokenType CommentType { get; } = comment.Type;

    internal string Value { get; } = comment.Value;

    internal override Type ILGenerator { get; } = null!;
}
