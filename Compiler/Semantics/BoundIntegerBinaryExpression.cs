namespace Sushi.Semantics;

/// <summary>
/// Represents a binary integer operation with concretely typed int32 operands.
/// </summary>
public sealed record BoundIntegerBinaryExpression : BoundExpression
{
    /// <summary>
    /// Gets the resolved binary integer operator.
    /// </summary>
    public IntegerBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the left operand, which is evaluated first.
    /// </summary>
    public BoundExpression Left { get; }

    /// <summary>
    /// Gets the right operand, including an independently typed shift count when applicable.
    /// </summary>
    public BoundExpression Right { get; }

    /// <summary>
    /// Creates a typed binary integer expression.
    /// </summary>
    /// <param name="operation">
    /// The integer operation.
    /// </param>
    /// <param name="left">
    /// The concrete int32 left operand.
    /// </param>
    /// <param name="right">
    /// The concrete int32 right operand.
    /// </param>
    public BoundIntegerBinaryExpression(IntegerBinaryOperator operation, BoundExpression left, BoundExpression right) : base(BoundType.Int32)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not BoundType.Int32 || right.Type is not BoundType.Int32)
        {
            throw new ArgumentException("Integer binary operations require int32 operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}