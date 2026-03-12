using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class NamespaceDeclarationNode([NotNull] Token token, [NotNull] NamespaceNode namespaceNode) : StatementNode
{
    public NamespaceNode Namespace { get; set; } = namespaceNode;

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        await this.Namespace.Verify(context);
    }
}
