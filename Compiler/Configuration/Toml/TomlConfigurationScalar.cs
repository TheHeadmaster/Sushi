using Sushi.Source;
using Tomlyn.Parsing;

namespace Sushi.Configuration.Toml;

public sealed class TomlConfigurationScalar(TomlParseEventKind kind, SourceSpan span) : TomlConfigurationValue(span)
{
    public TomlParseEventKind Kind { get; } = kind;
}