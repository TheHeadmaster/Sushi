using Sushi.Diagnostics;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    public List<SyntaxNode> Children { get; set; } = [];

    /// <summary>
    /// Contains the messages emitted by the parser, such as errors and warnings.
    /// </summary>
    public List<CompilerMessage> Messages { get; set; } = [];
    public override Token GetStartToken() => this.Children.First().GetStartToken();
}
