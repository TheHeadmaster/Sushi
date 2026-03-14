using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class UnaryExpressionNode([NotNull] Token token, bool isPrefix, ExpressionNode? operand) : ExpressionNode
{
    public OperatorType Operator { get; set; } = isPrefix
            ? token.Type switch
            {
                TokenType.Minus => OperatorType.Negative,
                _ => throw new NotImplementedException()
            }
            : token.Type switch
            {
                _ => throw new NotImplementedException()
            };

    public ExpressionNode? Operand { get; set; } = operand;

    public override Token? GetStartToken() => isPrefix ? token : this.Operand?.GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        if (this.Operand is not null)
        {
            await this.Operand.Verify(context);
        }
    }
}
