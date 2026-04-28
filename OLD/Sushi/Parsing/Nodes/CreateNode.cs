using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a create statement.
/// </summary>
/// <param name="token">
/// The starting token of the statement.
/// </param>
/// <param name="type">
/// The type to create an instance of.
/// </param>
/// <param name="arguments">
/// The list of arguments.
/// </param>
public sealed class CreateNode([NotNull] Token token, TypeNode? type, [NotNull] List<ExpressionNode> arguments) : ExpressionNode
{
    /// <summary>
    /// The type to create an instance of.
    /// </summary>
    public TypeNode? Type { get; set; } = type;

    /// <summary>
    /// The expression that resolves to the destroyer.
    /// </summary>
    public List<ExpressionNode> Arguments { get; set; } = arguments;

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}
