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
    public override Token? GetStartToken() => token;

    /// <inheritdoc />
    public override Token? GetEndToken() => token;

    /// <inheritdoc />
    public override List<CompilerMessage> GetMessages() => this.Messages;
}
