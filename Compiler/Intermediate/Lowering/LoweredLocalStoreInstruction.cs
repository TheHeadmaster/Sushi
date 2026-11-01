namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Writes a typed SSA value into a function-local storage slot. The same
/// operation represents initialization and later assignment.
/// </summary>
public sealed record LoweredLocalStoreInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-unique local slot index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the slot's concrete value type.
    /// </summary>
    public IRType Type { get; }

    /// <summary>
    /// Gets the value evaluated before this write.
    /// </summary>
    public LoweredValue Value { get; }

    /// <summary>
    /// Creates a typed local storage write.
    /// </summary>
    /// <param name="index">
    /// The destination local index.
    /// </param>
    /// <param name="type">
    /// The declared type of the destination slot.
    /// </param>
    /// <param name="value">
    /// The SSA value to store.
    /// </param>
    public LoweredLocalStoreInstruction(int index, IRType type, LoweredValue value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(value);

        if (!Enum.IsDefined(type) || type is IRType.Void || value.Type != type)
        {
            throw new ArgumentException("A local store requires a matching, non-void value.", nameof(value));
        }

        this.Index = index;
        this.Type = type;
        this.Value = value;
    }
}