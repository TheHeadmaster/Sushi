using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a binary expression, such as add or multiply.
/// </summary>
/// <param name="token">
/// The operator token of the expression.
/// </param>
/// <param name="left">
/// The left-hand side of the expression.
/// </param>
/// <param name="right">
/// The right-hand side of the expression.
/// </param>
public sealed class BinaryExpressionNode([NotNull] Token token, ExpressionNode? left, ExpressionNode? right) : ExpressionNode
{
    /// <summary>
    /// The binary operator being used.
    /// </summary>
    public OperatorType Operator { get; set; } = token.Type switch
    {
        TokenType.Plus => OperatorType.Add,
        TokenType.Minus => OperatorType.Subtract,
        TokenType.Asterisk => OperatorType.Multiply,
        TokenType.Slash => OperatorType.Divide,
        _ => throw new NotImplementedException()
    };

    /// <summary>
    /// The left-hand side of the expression.
    /// </summary>
    public ExpressionNode? Left { get; set; } = left;

    /// <summary>
    /// The right-hand side of the expression.
    /// </summary>
    public ExpressionNode? Right { get; set; } = right;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Left?.GetStartToken();
}
