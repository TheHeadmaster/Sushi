using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a file in the form of a <see cref="SyntaxNode"/>.
/// </summary>
/// <param name="filePath">
/// The path of the file.
/// </param>
/// <param name="fileName">
/// The name of the file.
/// </param>
/// <param name="statements">
/// The statements contained in the file.
/// </param>
public sealed class FileNode([NotNull] string filePath, [NotNull] string fileName, [NotNull] List<StatementNode> statements) : SyntaxNode
{
    /// <summary>
    /// The path of the file.
    /// </summary>
    public string FilePath { get; set; } = filePath;

    /// <summary>
    /// The name of the file.
    /// </summary>
    public string FileName { get; set; } = fileName;

    /// <summary>
    /// The statements contained in the file.
    /// </summary>
    public List<StatementNode> Statements { get; set; } = statements;

    /// <summary>
    /// The messages collected from the general parser.
    /// </summary>
    private readonly List<CompilerMessage> messages = [];

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        foreach (CompilerMessage message in this.messages)
        {
            yield return message;
        }

        foreach (StatementNode statement in this.Statements)
        {
            await foreach (CompilerMessage message in statement.GetMessages())
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        foreach (StatementNode statement in this.Statements)
        {
            await foreach (Token token in statement.GetTokens())
            {
                yield return token;
            }
        }
    }

    public Task AddMessage(CompilerMessage message)
    {
        this.messages.Add(message);

        return Task.CompletedTask;
    }
}
