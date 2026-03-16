using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Scope;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class NamespaceDeclarationNode([NotNull] Token token, ExpressionNode? body, [NotNull] ReferenceScope scope) : StatementNode
{
    public ExpressionNode? Body { get; set; } = body;

    public override Token GetStartToken() => token;

    public ReferenceScope Scope { get; set; } = scope;
    
    public override async Task Verify(VerificationContext context)
    {
        if (this.Body is not null)
        {
            await this.Body.Verify(context);
        }
    }
}
