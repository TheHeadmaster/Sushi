namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Computes the checked mathematical negation of a concrete int32 value.
/// </summary>
public sealed record LoweredIntegerNegateInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the integer value to negate.
    /// </summary>
    public LoweredValue Operand { get; }

    /// <summary>
    /// Creates a checked signed integer negation instruction.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the negated int32 value.
    /// </param>
    /// <param name="operand">
    /// The concrete int32 operand.
    /// </param>
    public LoweredIntegerNegateInstruction(LoweredTemporaryValue destination, LoweredValue operand)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(operand);

        if (destination.Type is not IRType.Int32 || operand.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer negation requires an int32 destination and operand.");
        }

        this.Destination = destination;
        this.Operand = operand;
    }
}