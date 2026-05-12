using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Marks a node as able to have modifiers, such as public or static.
/// </summary>
public interface IModifiable
{
    /// <summary>
    /// The modifiers of the node.
    /// </summary>
    public List<AccessModifier> Modifiers { get; set; }

    /// <summary>
    /// The token that describes the modifier.
    /// </summary>
    public List<Token> ModifierTokens { get; set; }

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