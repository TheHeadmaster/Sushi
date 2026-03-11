using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class UsingNode([NotNull] Token usingToken, NamespaceNode namespaceNode) : StatementNode
{
    public NamespaceNode Namespace { get; set; } = namespaceNode;

    public override Token GetStartToken() => usingToken;
}
