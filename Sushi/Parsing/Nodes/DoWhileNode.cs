using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class DoWhileNode([NotNull] Token token, ExpressionNode? condition, BlockNode? body) : StatementNode
{
    public ExpressionNode? Condition { get; set; } = condition;

    public BlockNode? Body { get; set; } = body;

    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        if (this.Condition is not null)
        {
            await this.Condition.Verify(context);
        }

        if (this.Body is not null)
        {
            await this.Body.Verify(context);
        }
    }
}
