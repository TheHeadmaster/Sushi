namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    public AbstractSyntaxTree() : base(null, new ReferenceScope(null))
    {
        foreach (string type in Constants.PrimitiveTypes.Values)
        {
            this.Scope.TryAddType(type); // We assume that this will succeed because its the first types that get registered
        }
    }

    /// <summary>
    /// The child nodes of the tree.
    /// </summary>
    public List<SyntaxNode> Children { get; } = [];
}
