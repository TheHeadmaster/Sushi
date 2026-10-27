namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Shifts a concrete integer while preserving runtime shift-count validation
/// and checked mathematical range semantics for left shifts.
/// </summary>
public sealed record LoweredIntegerShiftInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the shifted value.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the left-shift or right-shift operation.
    /// </summary>
    public IRIntegerBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the concrete integer value being shifted.
    /// </summary>
    public LoweredValue Left { get; }

    /// <summary>
    /// Gets the independently evaluated shift-count operand.
    /// </summary>
    public LoweredValue Right { get; }

    /// <summary>
    /// Creates an integer shift instruction with explicit operand types.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the int32 result.
    /// </param>
    /// <param name="operation">
    /// The left-shift or right-shift operation.
    /// </param>
    /// <param name="left">
    /// The int32 value to shift.
    /// </param>
    /// <param name="right">
    /// The int32 shift count, which must be between zero and 31 at execution.
    /// </param>
    public LoweredIntegerShiftInstruction(LoweredTemporaryValue destination, IRIntegerBinaryOperator operation, LoweredValue left, LoweredValue right)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operation is not (IRIntegerBinaryOperator.ShiftLeft or IRIntegerBinaryOperator.ShiftRight))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), "Only integer shift operators may use this instruction.");
        }

        if (destination.Type is not IRType.Int32 || left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("The current integer shift implementation requires int32 operands and an int32 destination.");
        }

        this.Destination = destination;
        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}