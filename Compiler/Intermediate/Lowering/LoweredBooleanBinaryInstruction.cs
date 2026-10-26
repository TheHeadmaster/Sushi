using Sushi.Intermediate;

namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Evaluates an eager Boolean binary operation and stores its result in a temporary.
/// </summary>
public sealed record LoweredBooleanBinaryInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the temporary value receiving the result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the resolved eager Boolean operation.
    /// </summary>
    public IRBooleanBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the first Boolean operand.
    /// </summary>
    public LoweredValue Left { get; }

    /// <summary>
    /// Gets the second Boolean operand.
    /// </summary>
    public LoweredValue Right { get; }

    /// <summary>
    /// Creates an eager Boolean instruction with concretely typed operands.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the Boolean result.
    /// </param>
    /// <param name="operation">
    /// The eager Boolean operator to execute.
    /// </param>
    /// <param name="left">
    /// The first Boolean operand.
    /// </param>
    /// <param name="right">
    /// The second Boolean operand.
    /// </param>
    public LoweredBooleanBinaryInstruction(LoweredTemporaryValue destination, IRBooleanBinaryOperator operation, LoweredValue left, LoweredValue right)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operation is not (IRBooleanBinaryOperator.EagerAnd or IRBooleanBinaryOperator.EagerOr or IRBooleanBinaryOperator.EagerXor))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), "Only eager Boolean operators may use this instruction.");
        }

        if (destination.Type is not IRType.Bool || left.Type is not IRType.Bool || right.Type is not IRType.Bool)
        {
            throw new ArgumentException("Eager Boolean operations require a Boolean destination and Boolean operands.");
        }

        this.Destination = destination;
        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}