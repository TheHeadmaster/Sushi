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
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        foreach (FileNode node in this.Children)
        {
            await foreach (CompilerMessage message in node.GetMessages())
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        foreach (FileNode node in this.Children)
        {
            await foreach (Token token in node.GetTokens())
            {
                yield return token;
            }
        }
    }
}