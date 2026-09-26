using Sushi.Source;

namespace Sushi.Configuration.Toml;

public sealed class TomlConfigurationArray(SourceSpan span) : TomlConfigurationValue(span)
{
    private readonly List<TomlConfigurationValue> items = [];

    public IReadOnlyList<TomlConfigurationValue> Items => this.items;

    public void Add(TomlConfigurationValue value) => this.items.Add(value);
}