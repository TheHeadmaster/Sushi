using Sushi.Source;
using Tomlyn.Parsing;

namespace Sushi.Configuration.Toml;

public sealed class TomlConfigurationScalar(SourceSpan span) : TomlConfigurationValue(span)
{
}