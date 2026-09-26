using System.Diagnostics.CodeAnalysis;
using Sushi.Source;

namespace Sushi.Configuration.Toml;

public sealed class TomlConfigurationTable(SourceSpan span) : TomlConfigurationValue(span)
{
    private readonly Dictionary<string, TomlConfigurationProperty> properties = [with(StringComparer.Ordinal)];

    public IReadOnlyDictionary<string, TomlConfigurationProperty> Properties => this.properties;

    public void Add([NotNull] TomlConfigurationProperty property) => this.properties.TryAdd(property.Name, property);

    public bool TryGetProperty(string name, out TomlConfigurationProperty property) => this.properties.TryGetValue(name, out property!);
}
