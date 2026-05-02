namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Marks a node as able to have an access modifier, such as public or protected.
/// </summary>
public interface IAccessModifiable
{
    /// <summary>
    /// The access modifier of the node.
    /// </summary>
    public AccessModifier AccessModifier { get; set; }

    /// <summary>
    /// Returns whether this node allows the specified modifier.
    /// </summary>
    /// <param name="modifier">
    /// The modifier to allow.
    /// </param>
    /// <returns>
    /// True if the modifier is allowed. False otherwise.
    /// </returns>
    public bool AllowsModifier(AccessModifier modifier);
}