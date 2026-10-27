namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Performs a fixed-width integer bitwise operation without arithmetic range checks.
/// </summary>
public sealed record LoweredIntegerBitwiseInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the bitwise result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the resolved binary bitwise operator.
    /// </summary>
    public IRIntegerBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the first concrete integer operand.
    /// </summary>
    public LoweredValue Left { get; }

    /// <summary>
    /// Gets the second concrete integer operand.
    /// </summary>
    public LoweredValue Right { get; }

    /// <summary>
    /// Creates a fixed-width integer bitwise instruction.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the int32 result.
    /// </param>
    /// <param name="operation">
    /// The bitwise AND, OR, or XOR operation.
    /// </param>
    /// <param name="left">
    /// The first int32 operand.
    /// </param>
    /// <param name="right">
    /// The second int32 operand.
    /// </param>
    public LoweredIntegerBitwiseInstruction(LoweredTemporaryValue destination, IRIntegerBinaryOperator operation, LoweredValue left, LoweredValue right)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operation is not (IRIntegerBinaryOperator.BitwiseAnd or IRIntegerBinaryOperator.BitwiseOr or IRIntegerBinaryOperator.BitwiseXor))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), "Only integer bitwise operators may use this instruction.");
        }

        if (destination.Type is not IRType.Int32 || left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer bitwise operations require an int32 destination and operands.");
        }

        this.Destination = destination;
        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}