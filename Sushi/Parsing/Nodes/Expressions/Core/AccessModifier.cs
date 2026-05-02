namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents a modifier to another node that changes its access restrictions.
/// </summary>
public enum AccessModifier
{
    /// <summary>
    /// This node is public, meaning it can be accessed anywhere.
    /// </summary>
    Public,

    /// <summary>
    /// This node is internal, meaning it can only be accessed within the same assembly.
    /// </summary>
    Internal,

    /// <summary>
    /// This node is private, meaning it can only be accessed by its parent node or its siblings.
    /// </summary>
    Private
}