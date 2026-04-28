using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a unary expression, such as an increment or NOT operator.
/// </summary>
/// <param name="token">
/// The start token of the expression.
/// </param>
/// <param name="isPrefix">
/// Whether the operator is a prefix operator.
/// </param>
/// <param name="operand">
/// The operand expression.
/// </param>
public class UnaryExpressionNode([NotNull] Token token, bool isPrefix, ExpressionNode? operand) : ExpressionNode
{
    /// <summary>
    /// Whether the operator is a prefix operator.
    /// </summary>
    public bool IsPrefix { get; set; } = isPrefix;

    /// <summary>
    /// The operator of the expression.
    /// </summary>
    public OperatorType Operator { get; set; } = isPrefix
            ? token.Type switch
            {
                TokenType.Minus => OperatorType.Negative,
                _ => throw new NotImplementedException()
            }
            : token.Type switch
            {
                _ => throw new NotImplementedException()
            };

    /// <summary>
    /// The operand expression.
    /// </summary>
    public ExpressionNode? Operand { get; set; } = operand;

    /// <inheritdoc />
    public override Token? GetStartToken() => isPrefix ? token : this.Operand?.GetStartToken();
}
