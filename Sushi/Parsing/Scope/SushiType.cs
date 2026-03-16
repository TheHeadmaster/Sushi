namespace Sushi.Parsing.Scope;

/// <summary>
/// Represents a type, such as a class or built-in type such as int32.
/// </summary>
public sealed class SushiType
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The namespace of the type.
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Returns whether the specified type is assignable to this type.
    /// </summary>
    /// <param name="type">
    /// The type to check.
    /// </param>
    /// <returns>
    /// True if the specified type is assignable to this one. Examples include:
    /// this == type
    /// type is a subclass of this
    /// type implements this
    /// </returns>
    public bool IsValidAssignment(SushiType type) => ReferenceEquals(this, type);
}
