using Sushi.Parsing.Nodes;

namespace Sushi.Parsing;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    public List<SyntaxNode> Children { get; set; } = [];
}
