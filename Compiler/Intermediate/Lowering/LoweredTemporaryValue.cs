namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Identifies a runtime SSA value produced by an instruction within a lowered function.
/// </summary>
public sealed record LoweredTemporaryValue : LoweredValue
{
    /// <summary>
    /// Gets the stable, zero-based temporary index, unique within its function.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a function-scoped reference to an instruction-produced runtime value.
    /// </summary>
    /// <param name="index">
    /// The temporary's index within the lowered function.
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