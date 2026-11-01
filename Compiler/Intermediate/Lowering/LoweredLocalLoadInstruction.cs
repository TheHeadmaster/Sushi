namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Reads the current contents of a function-local storage slot into a fresh
/// SSA temporary, preserving the value observed at this execution point.
/// </summary>
public sealed record LoweredLocalLoadInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the temporary receiving the loaded value.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the function-unique local slot index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a typed local load.
    /// </summary>
    /// <param name="destination">
    /// The new SSA temporary receiving the loaded value.
    /// </param>
    /// <param name="index">
    /// The local slot to read.
    /// </param>
    public LoweredLocalLoadInstruction(LoweredTemporaryValue destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (destination.Type is IRType.Void)
        {
            throw new ArgumentException("A local load must produce a concrete value.", nameof(destination));
        }

        this.Destination = destination;
        this.Index = index;
    }
}