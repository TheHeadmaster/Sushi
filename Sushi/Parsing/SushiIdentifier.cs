namespace Sushi.Parsing;

/// <summary>
/// Represents an identifier, such as a variable or parameter name.
/// </summary>
public sealed class SushiIdentifier
{
    /// <summary>
    /// The identifier name.
    /// </summary>
    public required string Name { get; set; }
}
