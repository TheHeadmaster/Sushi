namespace Sushi.Intermediate;

/// <summary>
/// Describes one function parameter in stable declaration order in structured and lowered IR.
/// </summary>
public sealed record IRParameter
{
    /// <summary>
    /// Gets the zero-based parameter index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the semantic parameter name for debugging and generated symbol readability.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter's concrete IR type.
    /// </summary>
    public IRType Type { get; }

    /// <summary>
    /// Creates a typed IR parameter declaration.
    /// </summary>
    /// <param name="index">
    /// The positional parameter index.
    /// </param>
    /// <param name="name">
    /// The semantic parameter name.
    /// </param>
    /// <param name="type">
    /// The concrete IR type.
    /// </param>
    public IRParameter(int index, string name, IRType type)
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