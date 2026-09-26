using Sushi.Source;

namespace Sushi.Configuration.Toml;

public abstract class TomlConfigurationValue(SourceSpan span)
{
    public SourceSpan Span { get; internal set; } = span;
}