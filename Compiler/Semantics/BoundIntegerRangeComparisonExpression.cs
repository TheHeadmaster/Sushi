namespace Sushi.Semantics;

/// <summary>
/// Preserves evaluation of a concrete integer operand when comparison
/// against an out-of-range exact constant has a predetermined result.
/// </summary>
public sealed record BoundIntegerRangeComparisonExpression : BoundExpression
{
    /// <summary>
    /// Gets the operand that must still be evaluated.
    /// </summary>
    public BoundExpression Operand { get; }

    /// <summary>
    /// Gets the mathematically predetermined Boolean result.
    /// </summary>
    public bool Result { get; }

    /// <summary>
    /// Creates a range-resolved comparison without discarding runtime evaluation.
    /// </summary>
    /// <param name="operand">
    /// The concrete integer expression whose evaluation must be preserved.
    /// </param>
    /// <param name="result">
    /// The comparison result for every representable int32 operand value.
    /// </param>
    public BoundIntegerRangeComparisonExpression(BoundExpression operand, bool result) : base(BoundType.Bool)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operand.Type is not BoundType.Int32)
        {
            throw new ArgumentException("The evaluated operand must have type int32.", nameof(operand));
        }

        this.Operand = operand;
        this.Result = result;
    }
}