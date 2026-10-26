namespace Sushi.Semantics;

/// <summary>
/// Represents a unary integer operation over a concrete int32 operand.
/// </summary>
public sealed record BoundIntegerUnaryExpression : BoundExpression
{
    /// <summary>
    /// Gets the resolved unary integer operator.
    /// </summary>
    public IntegerUnaryOperator Operator { get; }

    /// <summary>
    /// Gets the typed operand.
    /// </summary>
    public BoundExpression Operand { get; }

    /// <summary>
    /// Creates a typed unary integer expression.
    /// </summary>
    /// <param name="operation">
    /// The unary integer operation.
    /// </param>
    /// <param name="operand">
    /// The concrete int32 operand.
    /// </param>
    public BoundIntegerUnaryExpression(IntegerUnaryOperator operation, BoundExpression operand) : base(BoundType.Int32)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (operand.Type is not BoundType.Int32)
        {
            throw new ArgumentException("Integer unary operations require an int32 operand.", nameof(operand));
        }

        this.Operator = operation;
        this.Operand = operand;
    }
}