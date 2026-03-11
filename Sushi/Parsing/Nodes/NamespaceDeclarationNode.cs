using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class NamespaceDeclarationNode([NotNull] Token token, [NotNull] NamespaceNode namespaceNode) : StatementNode
{
    public NamespaceNode Namespace { get; set; } = namespaceNode;

    public override Token GetStartToken() => token;
}
