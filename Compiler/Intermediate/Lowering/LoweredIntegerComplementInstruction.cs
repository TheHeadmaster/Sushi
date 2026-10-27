namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Complements the 32 representation bits of a concrete signed integer value.
/// </summary>
public sealed record LoweredIntegerComplementInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the complemented value.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the concrete integer value whose bits are complemented.
    /// </summary>
    public LoweredValue Operand { get; }

    /// <summary>
    /// Creates a fixed-width integer complement instruction.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the int32 result.
    /// </param>
    /// <param name="operand">
    /// The int32 operand to complement.
    /// </param>
    public LoweredIntegerComplementInstruction(LoweredTemporaryValue destination, LoweredValue operand)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(operand);

        if (destination.Type is not IRType.Int32 || operand.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer complement requires an int32 destination and operand.");
        }

        this.Destination = destination;
        this.Operand = operand;
    }
}