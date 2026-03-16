namespace Sushi.Parsing.Scope;

/// <summary>
/// Represents a namespace, which is just a container that disambiguates conflicting names.
/// </summary>
public sealed class SushiNamespace
{
    /// <summary>
    /// The name of the namespace.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The parent namespace. Null if it is a root namespace.
    /// </summary>
    public SushiNamespace? Parent { get; set; }
}