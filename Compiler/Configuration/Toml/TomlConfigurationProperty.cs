using Sushi.Source;

namespace Sushi.Configuration.Toml;

public sealed record TomlConfigurationProperty(string Name, SourceSpan NameSpan, TomlConfigurationValue Value);