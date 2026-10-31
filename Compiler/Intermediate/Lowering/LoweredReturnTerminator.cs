
namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a function return that terminates a lowered basic block.
/// </summary>
public sealed record LoweredReturnTerminator : LoweredTerminator
{
    /// <summary>
    /// Gets the returned value, or null when the function returns without a result.
    /// </summary>
    public LoweredValue? Value { get; }

    /// <summary>
    /// Creates a return terminator for a function that supplies no result.
    /// </summary>
    public LoweredReturnTerminator()
    {
    }

    /// <summary>
    /// Creates a return terminator carrying a concrete value.
    /// </summary>
    /// <param name="value">
    /// The value to return.
    /// </param>
    public LoweredReturnTerminator(LoweredValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        this.Value = value;
    }
}
