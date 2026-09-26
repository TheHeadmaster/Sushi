using Sushi.Source;

namespace Sushi.Configuration.Toml;

public sealed class TomlConfigurationString(string value, SourceSpan span) : TomlConfigurationValue(span)
{
    public string Value { get; } = value;
}