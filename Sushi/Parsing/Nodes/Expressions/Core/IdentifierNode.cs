using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents an identifier, which is a named reference to another variable, member, or method.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that represents the identifier.
/// </param>
public sealed class IdentifierNode([NotNull] Token token) : ExpressionNode
{
    /// <summary>
    /// The name of the identifier.
    /// </summary>
    public string Name { get; set; } = token.Value;

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
