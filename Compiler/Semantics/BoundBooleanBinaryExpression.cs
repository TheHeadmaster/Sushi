namespace Sushi.Semantics;

/// <summary>
/// Represents a typed Boolean binary operation, retaining its evaluation policy.
/// </summary>
public sealed record BoundBooleanBinaryExpression : BoundExpression
{
    /// <summary>
    /// Gets the resolved Boolean operator.
    /// </summary>
    public BooleanBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the left operand, evaluated first.
    /// </summary>
    public BoundExpression Left { get; }

    /// <summary>
    /// Gets the right operand, which may be skipped for logical operators.
    /// </summary>
    public BoundExpression Right { get; }

    /// <summary>
    /// Creates a Boolean binary expression with concretely typed operands.
    /// </summary>
    /// <param name="operation">The built-in Boolean operation.</param>
    /// <param name="left">The left Boolean operand.</param>
    /// <param name="right">The right Boolean operand.</param>
    public BoundBooleanBinaryExpression(BooleanBinaryOperator operation, BoundExpression left, BoundExpression right) : base(BoundType.Bool)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not BoundType.Bool || right.Type is not BoundType.Bool)
        {
            throw new ArgumentException("Boolean binary operators require two Boolean operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}