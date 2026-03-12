using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class AssignmentNode([NotNull] IdentifierNode identifier, [NotNull] ExpressionNode right) : ExpressionNode
{
    public IdentifierNode Identifier { get; set; } = identifier;

    public ExpressionNode Right { get; set; } = right;

    public override Token GetStartToken() => this.Identifier.GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        await this.Identifier.Verify(context);

        await this.Right.Verify(context);
    }
}
