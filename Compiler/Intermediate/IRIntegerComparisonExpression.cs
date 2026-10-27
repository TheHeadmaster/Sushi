namespace Sushi.Intermediate;

/// <summary>
/// Represents a Boolean-valued comparison of concrete int32 operands.
/// </summary>
public sealed record IRIntegerComparisonExpression : IRExpression
{
    /// <summary>
    /// Gets the resolved comparison operation.
    /// </summary>
    public IRIntegerComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the first integer operand.
    /// </summary>
    public IRExpression Left { get; }

    /// <summary>
    /// Gets the second integer operand.
    /// </summary>
    public IRExpression Right { get; }

    /// <summary>
    /// Creates a typed integer comparison.
    /// </summary>
    /// <param name="operation">
    /// The mathematical comparison.
    /// </param>
    /// <param name="left">
    /// The first concrete int32 operand.
    /// </param>
    /// <param name="right">
    /// The second concrete int32 operand.
    /// </param>
    public IRIntegerComparisonExpression(IRIntegerComparisonOperator operation, IRExpression left, IRExpression right) : base(IRType.Bool)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer comparisons require concrete int32 operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}