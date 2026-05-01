using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    /// <summary>
    /// The child file nodes in the tree. Each one of these nodes represents a source file.
    /// </summary>
    public List<FileNode> Children { get; set; } = [];

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Children.FirstOrDefault()?.GetStartToken();

    /// <inheritdoc />
    public override Token? GetEndToken() => this.Children.LastOrDefault()?.GetEndToken();

    /// <inheritdoc/>
    public override List<CompilerMessage> AggregateMessages() => [.. this.Messages.Concat(this.Children.SelectMany(y => y.AggregateMessages()))];
}