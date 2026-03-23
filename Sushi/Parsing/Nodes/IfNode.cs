using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class IfNode([NotNull] Token token, ExpressionNode? condition, [NotNull] BlockNode body, IfNode? elseNode) : StatementNode
{
    public ExpressionNode? Condition { get; set; } = condition;

    public BlockNode Body { get; set; } = body;

    public IfNode? Else { get; set; } = elseNode;

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        if (this.Condition is not null)
        {
            await this.Condition.Verify(context);
        }

        await this.Body.Verify(context);

        if (this.Else is not null)
        {
            await this.Else.Verify(context);
        }
    }
}
