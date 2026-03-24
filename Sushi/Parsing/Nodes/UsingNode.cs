using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a using statement.
/// </summary>
/// <param name="token">
/// The token that starts the using statement.
/// </param>
/// <param name="identifier">
/// The identifier expression.
/// </param>
public sealed class UsingNode([NotNull] Token token, ExpressionNode? identifier) : StatementNode
{
    /// <summary>
    /// The identifier expression.
    /// </summary>
    public ExpressionNode? Identifier { get; set; } = identifier;

    /// <summary>
    /// Contains the resolved namespaces expanded from the using statement.
    /// </summary>
    public List<string> ResolvedNamespaces { get; set; } = [];

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
        ExpressionNode? currentNode = this.Identifier;

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
