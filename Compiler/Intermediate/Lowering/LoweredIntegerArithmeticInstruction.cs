namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Evaluates a checked binary int32 arithmetic operation, preserving its
/// runtime range and zero-divisor failure requirements.
/// </summary>
public sealed record LoweredIntegerArithmeticInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the resolved arithmetic operation.
    /// </summary>
    public IRIntegerBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the left operand, evaluated first.
    /// </summary>
    public LoweredValue Left { get; }

    /// <summary>
    /// Gets the right operand, evaluated second.
    /// </summary>
    public LoweredValue Right { get; }

    /// <summary>
    /// Creates a checked integer arithmetic instruction with concrete operands.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the int32 result.
    /// </param>
    /// <param name="operation">
    /// The arithmetic operation to execute.
    /// </param>
    /// <param name="left">
    /// The first int32 operand.
    /// </param>
    /// <param name="right">
    /// The second int32 operand.
    /// </param>
    public LoweredIntegerArithmeticInstruction(LoweredTemporaryValue destination, IRIntegerBinaryOperator operation, LoweredValue left, LoweredValue right)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operation is not (IRIntegerBinaryOperator.Add or IRIntegerBinaryOperator.Subtract or IRIntegerBinaryOperator.Multiply or IRIntegerBinaryOperator.Divide or IRIntegerBinaryOperator.Remainder))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), "Only arithmetic operators may use this lowered instruction.");
        }

        if (destination.Type is not IRType.Int32 || left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer arithmetic requires an int32 destination and int32 operands.");
        }

        this.Destination = destination;
        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}