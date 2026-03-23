using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class NamespaceNode(IdentifierNode? identifier, ExpressionNode? right) : ExpressionNode
{
    public ExpressionNode? Right { get; set; } = right;

    public IdentifierNode? Name { get; set; } = identifier;

    public override Token GetStartToken() => this.Name.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        if (this.Right is not null)
        {
            await this.Right.Verify(context);
        }

        if (this.Name is not null)
        {
            await this.Name.Verify(context);
        }
    }
}
