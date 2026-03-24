using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    /// <summary>
    /// The child file nodes in the tree.
    /// </summary>
    public List<FileNode> Children { get; set; } = [];

    /// <summary>
    /// Contains the messages emitted by the parser, such as errors and warnings.
    /// </summary>
    public List<CompilerMessage> Messages { get; set; } = [];

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Children.FirstOrDefault()?.GetStartToken();
}
