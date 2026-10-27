namespace Sushi.Intermediate;

/// <summary>
/// Evaluates a concrete integer expression before producing a known
/// Boolean comparison result.
/// </summary>
public sealed record IRIntegerRangeComparisonExpression : IRExpression
{
    /// <summary>
    /// Gets the integer expression whose runtime behavior must be preserved.
    /// </summary>
    public IRExpression Operand { get; }

    /// <summary>
    /// Gets the Boolean result determined during semantic binding.
    /// </summary>
    public bool Result { get; }

    /// <summary>
    /// Creates a range-resolved comparison with mandatory operand evaluation.
    /// </summary>
    /// <param name="operand">
    /// The concrete integer expression to evaluate.
    /// </param>
    /// <param name="result">
    /// The mathematical comparison result.
    /// </param>
    public IRIntegerRangeComparisonExpression(IRExpression operand, bool result) : base(IRType.Bool)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operand.Type is not IRType.Int32)
        {
            throw new ArgumentException("The evaluated operand must have type int32.", nameof(operand));
        }

        this.Operand = operand;
        this.Result = result;
    }
}