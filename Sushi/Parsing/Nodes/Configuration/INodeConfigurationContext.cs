using System.Linq.Expressions;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Configuration;

/// <summary>
/// The context used to configure a <see cref="SyntaxNode"/>.
/// </summary>
public interface INodeConfigurationContext<TNode> where TNode : SyntaxNode
{
    /// <summary>
    /// Configures the node to have access modifiers enabled.
    /// </summary>
    /// <param name="accessModifiers">
    /// The list of access modifiers allowed by the node.
    /// </param>
    /// <returns>
    /// The <see cref="IAccessModifierConfiguration"/> to further configure access modifiers.
    /// </returns>
    public IAccessModifierConfiguration HasAccess(params AccessModifier[] accessModifiers);

    /// <summary>
    /// Configures the node to have the static modifier enabled.
    /// </summary>
    /// <param name="value">
    /// True if the static modifier should be enabled.
    /// </param>
    /// <returns>
    /// The <see cref="INodeConfigurationContext{TNode}"/>.
    /// </returns>
    public INodeConfigurationContext<TNode> HasStatic(bool value = true);

    /// <summary>
    /// Configures the node to have a specified order for its modifiers.
    /// </summary>
    /// <returns>
    /// The <see cref="IModifierOrderConfiguration"/> to further configure the order.
    /// </returns>
    public IModifierOrderConfiguration HasModifierOrder();

    public IChildConfiguration HasChild<TProperty>(Expression<Func<TNode, TProperty>> nodeExpression);
    public IChildConfiguration HasChildren<TProperty>(Expression<Func<TNode, IEnumerable<TProperty>>> nodeExpression);
    
    
    public ITokenConfiguration<TNode> HasToken(Expression<Func<TNode, Token?>> token);
}
