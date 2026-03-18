using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class BinaryExpressionNode([NotNull] Token token, [NotNull] ExpressionNode left, [NotNull] ExpressionNode right) : ExpressionNode
{
    public OperatorType Operator { get; set; } = token.Type switch
    {
        TokenType.Plus => OperatorType.Add,
        TokenType.Minus => OperatorType.Subtract,
        TokenType.Asterisk => OperatorType.Multiply,
        TokenType.Slash => OperatorType.Divide,
        _ => throw new NotImplementedException()
    };

    public ExpressionNode Left { get; set; } = left;

    public ExpressionNode Right { get; set; } = right;

    public override Token? GetStartToken() => this.Left.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        if (this.Left is not null)
        {

            await this.Left.Verify(context);
        }

        if (this.Right is not null)
        {
            await this.Right.Verify(context);
        }
    }
}
