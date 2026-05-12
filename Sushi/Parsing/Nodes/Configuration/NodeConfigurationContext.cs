using System.Linq.Expressions;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Configuration;

/// <summary>
/// The implementation of <see cref="INodeConfigurationContext"/>.
/// </summary>
public sealed class NodeConfigurationContext<TNode> : INodeConfigurationContext<TNode>, IAccessModifierConfiguration where TNode: SyntaxNode
{
    /// <summary>
    /// The list of allowed access modifiers.
    /// </summary>
    public List<AccessModifier> AllowedAccessModifiers { get; private set; } = [];

    /// <summary>
    /// Whether the access modifier is required.
    /// </summary>
    public bool IsAccessModifierRequired { get; private set; }

    /// <summary>
    /// Whether there can only be one access modifier.
    /// </summary>
    public bool IsAccessModifierSingle { get; private set; }

    /// <summary>
    /// Whether the static modifier is allowed.
    /// </summary>
    public bool HasStaticModifier { get; private set; }

    /// <inheritdoc />
    public IAccessModifierConfiguration HasAccess(params AccessModifier[] accessModifiers)
    {
        this.AllowedAccessModifiers = [.. accessModifiers];

        return this;
    }

    public IChildConfiguration HasChild<TProperty>(Expression<Func<TNode, TProperty>> nodeExpression) => throw new NotImplementedException();
    public IChildConfiguration HasChildren<TProperty>(Expression<Func<TNode, IEnumerable<TProperty>>> nodeExpression) => throw new NotImplementedException();
    public IModifierOrderConfiguration HasModifierOrder() => throw new NotImplementedException();

    public INodeConfigurationContext<TNode> HasStatic(bool value = true)
    {
        this.HasStaticModifier = value;

        return this;
    }

    public INodeConfigurationContext<TNode> HasToken(Expression<Func<TNode, Token?>> token) => throw new NotImplementedException();

    /// <inheritdoc />
    public IAccessModifierConfiguration IsRequired(bool value = true)
    {
        this.IsAccessModifierRequired = value;

        return this;
    }

    /// <inheritdoc />
    public IAccessModifierConfiguration IsSingle(bool value = true)
    {
        this.IsAccessModifierSingle = value;

        return this;
    }
}
