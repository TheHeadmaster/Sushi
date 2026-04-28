using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a destroy statement.
/// </summary>
/// <param name="token">
/// The starting token of the statement.
/// </param>
/// <param name="obj">
/// The identifier of the object to destroy.
/// </param>
/// <param name="destroyer">
/// The expression that resolves to the destroyer.
/// </param>
public sealed class DestroyNode([NotNull] Token token, IdentifierNode? obj, ExpressionNode? destroyer) : StatementNode
{
    /// <summary>
    /// The identifier of the object to destroy.
    /// </summary>
    public IdentifierNode? Object { get; set; } = obj;

    /// <summary>
    /// The expression that resolves to the destroyer.
    /// </summary>
    public ExpressionNode? Destroyer { get; set; } = destroyer;

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}
