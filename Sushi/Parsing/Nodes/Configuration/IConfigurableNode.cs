using System.Diagnostics.CodeAnalysis;

namespace Sushi.Parsing.Nodes.Configuration;

/// <summary>
/// Represents a node that can be configured to behave a certain way using fluent syntax.
/// </summary>
public interface IConfigurableNode<TNode> where TNode : SyntaxNode
{
    /// <summary>
    /// Configures the node.
    /// </summary>
    /// <param name="context">
    /// The context used to configure the node.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task Configure([NotNull] INodeConfigurationContext<TNode> context);

    public Task Parse([NotNull] IParserConfigurationContext<TNode> context);
}