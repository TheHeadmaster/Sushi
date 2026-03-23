using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Parsers;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class DestroyNode([NotNull] Token token, [NotNull] IdentifierNode obj, ExpressionNode? destroyer) : StatementNode
{
    public IdentifierNode Object { get; set; } = obj;

    public ExpressionNode? Destroyer { get; set; } = destroyer;

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        await this.Object.Verify(context);

        if (this.Destroyer is not null)
        {
            await this.Destroyer.Verify(context);
        }
    }
}
