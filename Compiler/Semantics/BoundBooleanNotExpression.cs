namespace Sushi.Semantics;

/// <summary>
/// Represents logical negation of a bound Boolean operand.
/// </summary>
public sealed record BoundBooleanNotExpression : BoundExpression
{
    /// <summary>
    /// Gets the Boolean operand whose value is negated.
    /// </summary>
    public BoundExpression Operand { get; }

    /// <summary>
    /// Creates a typed Boolean negation expression.
    /// </summary>
    /// <param name="operand">The bound Boolean operand.</param>
    public BoundBooleanNotExpression(BoundExpression operand) : base(BoundType.Bool)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operand.Type is not BoundType.Bool)
        {
            throw new ArgumentException("Logical negation requires a Boolean operand.", nameof(operand));
        }

        this.Operand = operand;
    }
}