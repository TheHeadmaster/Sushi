using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class IfNode([NotNull] Token token, ExpressionNode? condition, [NotNull] BlockNode body, IfNode? elseNode) : StatementNode
{
    public ExpressionNode? Condition { get; set; } = condition;

    public BlockNode Body { get; set; } = body;

    public IfNode? Else { get; set; } = elseNode;

    public override Token GetStartToken() => token;
}
