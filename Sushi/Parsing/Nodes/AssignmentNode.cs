using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an assignment of an expression to a variable.
/// </summary>
/// <param name="identifier">
/// The identifier on the left-hand side.
/// </param>
/// <param name="right">
/// The expression on the right-hand side.
/// </param>
public sealed class AssignmentNode(IdentifierNode? identifier, ExpressionNode? right) : ExpressionNode
{
    /// <summary>
    /// The identifier on the left-hand side.
    /// </summary>
    public IdentifierNode? Identifier { get; set; } = identifier;

    /// <summary>
    /// The expression on the right-hand side.
    /// </summary>
    public ExpressionNode? Right { get; set; } = right;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Identifier?.GetStartToken();
}
