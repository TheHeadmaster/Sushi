using System.Collections.ObjectModel;

namespace Sushi;

/// <summary>
/// Holds constants used accross the entire application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Contains keywords reserved by the language, and therefore cannot be used as identifiers.
    /// </summary>
    public static ReadOnlyCollection<string> ReservedKeywords { get; } =
    [
        "bool",
        "true",
        "false",
        "i32",
        "f32"
    ];
}
