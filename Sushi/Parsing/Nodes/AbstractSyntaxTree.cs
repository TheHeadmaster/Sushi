namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree() : SyntaxNode(null)
{
    /// <summary>
    /// The child nodes of the tree.
    /// </summary>
    public List<SyntaxNode> Children { get; } = [];
}
