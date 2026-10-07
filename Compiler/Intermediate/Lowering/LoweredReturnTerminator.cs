namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a function return that terminates a lowered basic block.
/// </summary>
public sealed record LoweredReturnTerminator : LoweredTerminator
{
    /// <summary>
    /// Gets the value returned by the function.
    /// </summary>
    public LoweredValue Value { get; }

    /// <summary>
    /// Creates a lowered return terminator.
    /// </summary>
    /// <param name="value">
    /// The value returned by the function.
    /// </param>
    public LoweredReturnTerminator(LoweredValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.Value = value;
    }
}