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
        "int32",
        "float32"
    ];

    public static ReadOnlyDictionary<string, string> PrimitiveTypes { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> {
        { "bool", "Boolean" },
        { "int32", "Int32" },
        { "float32", "Float32" },
    });

    public static ReadOnlyDictionary<string, string> SushiToCTypes { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> {

        { "__MAIN_SHADOWED_INT_SPECIAL", "int" },
        { "Boolean", "bool" },
        { "Int32", "int32_t" },
        { "Float32", "float" },
    });

    public static ReadOnlyCollection<string> BooleanLiterals { get; } =
    [
        "true",
        "false"
    ];
}
