namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Produces the logical negation of a Boolean runtime value.
/// </summary>
public sealed record LoweredBooleanNotInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the temporary value that receives the result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the Boolean value to negate.
    /// </summary>
    public LoweredValue Operand { get; }

    /// <summary>
    /// Creates a typed Boolean negation instruction.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the Boolean result.
    /// </param>
    /// <param name="operand">
    /// The Boolean input value.
    /// </param>
    public LoweredBooleanNotInstruction(LoweredTemporaryValue destination, LoweredValue operand)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(operand);

        if (destination.Type is not IRType.Bool || operand.Type is not IRType.Bool)
        {
            throw new ArgumentException("Boolean negation requires a Boolean destination and operand.");
        }

        this.Destination = destination;
        this.Operand = operand;
    }
}