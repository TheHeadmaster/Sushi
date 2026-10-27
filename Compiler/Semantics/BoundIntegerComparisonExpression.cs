namespace Sushi.Semantics;

/// <summary>
/// Compares two concrete int32 expressions and produces a Boolean result.
/// </summary>
public sealed record BoundIntegerComparisonExpression : BoundExpression
{
    /// <summary>
    /// Gets the resolved mathematical comparison.
    /// </summary>
    public IntegerComparisonOperator Operator { get; }

    /// <summary>
    /// Gets the left operand, evaluated first.
    /// </summary>
    public BoundExpression Left { get; }

    /// <summary>
    /// Gets the right operand, evaluated second.
    /// </summary>
    public BoundExpression Right { get; }

    /// <summary>
    /// Creates a comparison between concrete int32 operands.
    /// </summary>
    /// <param name="operation">
    /// The comparison to perform.
    /// </param>
    /// <param name="left">
    /// The first concrete integer operand.
    /// </param>
    /// <param name="right">
    /// The second concrete integer operand.
    /// </param>
    public BoundIntegerComparisonExpression(IntegerComparisonOperator operation, BoundExpression left, BoundExpression right) : base(BoundType.Bool)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not BoundType.Int32 || right.Type is not BoundType.Int32)
        {
            throw new ArgumentException("Concrete integer comparisons require int32 operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}