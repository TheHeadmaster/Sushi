using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Writes the constant value.
/// </summary>
/// <param name="token">
/// The constant token.
/// </param>
public sealed class ConstantNode([NotNull] Token token) : ExpressionNode
{
    /// <summary>
    /// The constant value.
    /// </summary>
    public string Value { get; set; } = token.Value;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;
}