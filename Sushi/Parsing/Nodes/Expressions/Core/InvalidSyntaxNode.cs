using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents a node that is just an unknown token from an invalid syntax span from the lexing step. Used to keep
/// the parsing mechanism from throwing exceptions and gracefully handling errors.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that represents the invalid syntax span.
/// </param>
public sealed class InvalidSyntaxNode([NotNull] Token token) : ExpressionNode
{
    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        yield break;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        yield return token;
    }
}
