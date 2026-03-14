using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class UsingNode([NotNull] Token usingToken, ExpressionNode? identifier) : StatementNode
{
    public ExpressionNode Identifier { get; set; } = identifier;

    public override Token GetStartToken() => usingToken;

    public override async Task Verify(VerificationContext context)
    {
        await this.Identifier.Verify(context);
    }
}
