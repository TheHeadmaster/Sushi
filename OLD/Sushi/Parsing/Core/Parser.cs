using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.TopLevelStatements;
using Sushi.Precompilation;
using Sushi.Tokenization;

namespace Sushi.Parsing.Core;

/// <summary>
/// Handles parsing a <see cref="List{T}"/> of <see cref="TokenFile"/> objects.
/// </summary>
public sealed class Parser
{

    /// <summary>
    /// Uses a snippet of tokens as the source instead of a source file. Mostly used for testing.
    /// </summary>
    /// <param name="tokens">
    /// The tokens that comprise the snippet.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task UseSnippet([NotNull] List<Token> tokens)
    {
        this.tokens = tokens;
        this.currentIndex = 0;

        return Task.CompletedTask;
    }
}