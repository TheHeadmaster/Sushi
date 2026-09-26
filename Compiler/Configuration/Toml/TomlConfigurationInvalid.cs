using Sushi.Source;

namespace Sushi.Configuration.Toml;
public sealed class TomlConfigurationInvalid(SourceSpan span) : TomlConfigurationValue(span);