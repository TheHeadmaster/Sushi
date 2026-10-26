namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Selects the next basic block using a runtime Boolean condition.
/// Only the selected successor executes.
/// </summary>
public sealed record LoweredConditionalBranchTerminator : LoweredTerminator
{
    /// <summary>
    /// Gets the Boolean value controlling the branch.
    /// </summary>
    public LoweredValue Condition { get; }

    /// <summary>
    /// Gets the destination selected when the condition is true.
    /// </summary>
    public string TrueTarget { get; }

    /// <summary>
    /// Gets the destination selected when the condition is false.
    /// </summary>
    public string FalseTarget { get; }

    /// <summary>
    /// Creates a conditional branch with two explicit destinations.
    /// </summary>
    /// <param name="condition">
    /// The Boolean runtime value selecting the successor.
    /// </param>
    /// <param name="trueTarget">
    /// The destination selected for true.
    /// </param>
    /// <param name="falseTarget">
    /// The destination selected for false.
    /// </param>
    public LoweredConditionalBranchTerminator(LoweredValue condition, string trueTarget, string falseTarget)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrEmpty(trueTarget);
        ArgumentException.ThrowIfNullOrEmpty(falseTarget);

        if (condition.Type is not IRType.Bool)
        {
            throw new ArgumentException("A conditional branch requires a Boolean condition.", nameof(condition));
        }

        this.Condition = condition;
        this.TrueTarget = trueTarget;
        this.FalseTarget = falseTarget;
    }
}