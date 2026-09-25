using Sushi.Diagnostics;
using Sushi.Source;
using Tomlyn.Syntax;

namespace Sushi.Configuration;

public sealed record TomlConfigurationParseResult(SourceSnapshot Snapshot, DocumentSyntax Syntax, IReadOnlyList<SushiDiagnostic> Diagnostics);