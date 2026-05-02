namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Marks a node as able to be static.
/// </summary>
public interface ICanBeStatic
{
    /// <summary>
    /// Whether the node is static or not. True if static, false otherwise.
    /// </summary>
    public bool IsStatic { get; set; }
}