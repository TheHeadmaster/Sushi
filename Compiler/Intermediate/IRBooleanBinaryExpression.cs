namespace Sushi.Intermediate;

/// <summary>
/// Represents a Boolean binary operation, preserving its operand order
/// and short-circuit or eager evaluation semantics.
/// </summary>
public sealed record IRBooleanBinaryExpression : IRExpression
{
    /// <summary>
    /// Gets the Boolean operation.
    /// </summary>
    public IRBooleanBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the first operand.
    /// </summary>
    public IRExpression Left { get; }

    /// <summary>
    /// Gets the second operand.
    /// </summary>
    public IRExpression Right { get; }

    /// <summary>
    /// Creates a typed Boolean binary operation.
    /// </summary>
    /// <param name="operation">
    /// The resolved Boolean operation.
    /// </param>
    /// <param name="left">
    /// The first Boolean operand.
    /// </param>
    /// <param name="right">
    /// The second Boolean operand.
    /// </param>
    public IRBooleanBinaryExpression(IRBooleanBinaryOperator operation, IRExpression left, IRExpression right) : base(IRType.Bool)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not IRType.Bool || right.Type is not IRType.Bool)
        {
            throw new ArgumentException("Boolean binary operations require two Boolean operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}