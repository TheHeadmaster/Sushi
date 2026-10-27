namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Produces a Boolean result by comparing concrete signed int32 values.
/// </summary>
public sealed record LoweredIntegerComparisonInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the Boolean temporary receiving the comparison result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the comparison operation.
    /// </summary>
    public IRIntegerComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the first integer operand.
    /// </summary>
    public LoweredValue Left { get; }

    /// <summary>
    /// Gets the second integer operand.
    /// </summary>
    public LoweredValue Right { get; }

    /// <summary>
    /// Creates a typed integer comparison instruction.
    /// </summary>
    /// <param name="destination">
    /// The Boolean temporary receiving the result.
    /// </param>
    /// <param name="operation">
    /// The signed integer comparison.
    /// </param>
    /// <param name="left">
    /// The first int32 operand.
    /// </param>
    /// <param name="right">
    /// The second int32 operand.
    /// </param>
    public LoweredIntegerComparisonInstruction(LoweredTemporaryValue destination, IRIntegerComparisonOperator operation, LoweredValue left, LoweredValue right)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (destination.Type is not IRType.Bool || left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer comparisons require int32 operands and a Boolean destination.");
        }

        this.Destination = destination;
        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}