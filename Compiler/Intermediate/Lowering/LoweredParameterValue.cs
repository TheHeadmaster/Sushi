namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Identifies a runtime function argument that is already an SSA value at the LLVM function boundary.
/// </summary>
public sealed record LoweredParameterValue : LoweredValue
{
    /// <summary>
    /// Gets the zero-based source function parameter position.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a typed runtime parameter reference.
    /// </summary>
    /// <param name="index">
    /// The parameter position.
    /// </param>
    /// <param name="type">
    /// The concrete value type.
    /// </param>
    public LoweredParameterValue(int index, IRType type) : base(type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
    }
}