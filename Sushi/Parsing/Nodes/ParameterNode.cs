using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ParameterNode(TypeNode? type, IdentifierNode? identifier) : StatementNode
{
    public TypeNode? Type { get; set; } = type;

    public IdentifierNode? Name { get; set; } = identifier;

    public override Token? GetStartToken() => this.Type?.GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        if (this.Type is not null)
        {
            await this.Type.Verify(context);
        }

        if (this.Name is not null)
        {
            await this.Name.Verify(context);
        }
    }
}
