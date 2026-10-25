namespace Sushi.Intermediate;

/// <summary>
/// Represents Boolean negation before lowering.
/// </summary>
public sealed record IRBooleanNotExpression : IRExpression
{
    /// <summary>
    /// Gets the Boolean operand.
    /// </summary>
    public IRExpression Operand { get; }

    /// <summary>
    /// Creates a typed Boolean negation operation.
    /// </summary>
    /// <param name="operand">
    /// The expression whose result is negated.
    /// </param>
    public IRBooleanNotExpression(IRExpression operand) : base(IRType.Bool)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operand.Type is not IRType.Bool)
        {
            throw new ArgumentException("Boolean negation requires a Boolean operand.", nameof(operand));
        }

        this.Operand = operand;
    }
}