using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ExpressionStatementNode(ExpressionNode? expression) : StatementNode
{
    public ExpressionNode? Expression { get; set; } = expression;

    public override Token? GetStartToken() => this.Expression?.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        if (this.Expression is not null)
        {
            await this.Expression.Verify(context);
        }
    }
}
