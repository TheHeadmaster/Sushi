using System.Collections.ObjectModel;

namespace Sushi;

public static class Constants
{
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
