namespace Sushi.Semantics;

/// <summary>
/// Identifies a declared function parameter by its stable, zero-based ordinal and semantic name.
/// </summary>
public sealed record BoundParameter
{
    /// <summary>
    /// Gets the zero-based position of this parameter in its declaring function.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the decoded semantic name used for source-level references and named arguments.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter's concrete built-in type.
    /// </summary>
    public BoundType Type { get; }

    /// <summary>
    /// Creates a bound parameter with validated identity and type.
    /// </summary>
    /// <param name="index">
    /// The zero-based parameter position.
    /// </param>
    /// <param name="name">
    /// The source identifier after unescaping.
    /// </param>
    /// <param name="type">
    /// The resolved concrete parameter type.
    /// </param>
    public BoundParameter(int index, string name, BoundType type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
        this.Name = name;
        this.Type = type;
    }
}