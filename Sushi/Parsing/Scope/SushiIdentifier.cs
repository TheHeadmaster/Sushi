namespace Sushi.Parsing.Scope;

/// <summary>
/// Represents an identifier, such as a variable or parameter name.
/// </summary>
public sealed class SushiIdentifier
{
    /// <summary>
    /// The identifier name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The identifier type.
    /// </summary>
    public required string Type { get; set; }
}