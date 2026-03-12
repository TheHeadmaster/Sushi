using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class DoWhileNode([NotNull] Token token, ExpressionNode? condition, [NotNull] BlockNode body) : StatementNode
{
    public ExpressionNode? Condition { get; set; } = condition;

    public BlockNode Body { get; set; } = body;

    public override Token GetStartToken() => token;
}
