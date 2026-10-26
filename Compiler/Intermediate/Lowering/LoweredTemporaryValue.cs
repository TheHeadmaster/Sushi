namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Identifies a runtime value produced by an instruction within a lowered basic block.
/// </summary>
public sealed record LoweredTemporaryValue : LoweredValue
{
    /// <summary>
    /// Gets the stable, zero-based index identifying this temporary value.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a reference to an instruction-produced runtime value.
    /// </summary>
    /// <param name="index">
    /// The temporary's index within the lowered basic block.
    /// </param>
    /// <param name="type">
    /// The concrete type of the produced value.
    /// </param>
    public LoweredTemporaryValue(int index, IRType type) : base(type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
    }
}