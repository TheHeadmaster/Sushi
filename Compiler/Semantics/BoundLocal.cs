namespace Sushi.Semantics;

/// <summary>
/// Identifies an initialized local by its function-unique declaration index.
/// Its identity remains distinct from parameter indices and source spelling.
/// </summary>
public sealed record BoundLocal
{
    /// <summary>
    /// Gets the declaration index within the enclosing function.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the source-level binding name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the local's concrete value type.
    /// </summary>
    public BoundType Type { get; }

    /// <summary>
    /// Creates a local identity with a concrete, non-void type.
    /// </summary>
    /// <param name="index">
    /// The unique declaration index within the function.
    /// </param>
    /// <param name="name">
    /// The decoded semantic name.
    /// </param>
    /// <param name="type">
    /// The local's declared value type.
    /// </param>
    public BoundLocal(int index, string name, BoundType type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!Enum.IsDefined(type) || type is BoundType.Void)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
        this.Name = name;
        this.Type = type;
    }
}