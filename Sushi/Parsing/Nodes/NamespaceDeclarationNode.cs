using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a namespace declaration.
/// </summary>
/// <param name="token">
/// The starting token of the namespace declaration.
/// </param>
/// <param name="body">
/// The expression body of the namespace.
/// </param>
public sealed class NamespaceDeclarationNode([NotNull] Token token, ExpressionNode? body) : StatementNode
{
    /// <summary>
    /// The expression body of the namespace.
    /// </summary>
    public ExpressionNode? Body { get; set; } = body;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;

    /// <summary>
    /// Builds a namespace chain from this using node's namespace expression.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns a <see cref="List{T}"/> of <see cref="string"/> objects.
    /// </returns>
    public async Task<List<string>> BuildNamespace()
    {
        ExpressionNode? currentNode = this.Body;

        List<string> namespaceChain = [];

        while (true)
        {
            if (currentNode is null)
            {
                break;
            }

            currentNode = await ConsumeNamespaceOrIdentifier(currentNode, namespaceChain);
        }

        return namespaceChain;
    }

    /// <summary>
    /// Consumes a namespace or identifier node and adds the namespace part to the chain.
    /// </summary>
    /// <param name="node">
    /// The node to consume.
    /// </param>
    /// <param name="namespaceChain">
    /// The namespace chain to modify.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the next node in the chain.
    /// </returns>
    private static Task<ExpressionNode?> ConsumeNamespaceOrIdentifier([NotNull] ExpressionNode node, [NotNull] List<string> namespaceChain)
    {
        ExpressionNode? nextNode = null;

        if (node is NamespaceNode namespaceNode && namespaceNode.Name is not null)
        {
            namespaceChain.Add(namespaceNode.Name.Name);
            nextNode = namespaceNode.Right;
        }
        else if (node is IdentifierNode identifier)
        {
            namespaceChain.Add(identifier.Name);
            nextNode = null;
        }

        return Task.FromResult(nextNode);
    }
}
