using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class UsingNode([NotNull] Token usingToken, NamespaceNode namespaceNode) : StatementNode
{
    public NamespaceNode Namespace { get; set; } = namespaceNode;

    public override Token GetStartToken() => usingToken;

    public override async Task Verify(VerificationContext context)
    {
        await this.Namespace.Verify(context);
    }
}
