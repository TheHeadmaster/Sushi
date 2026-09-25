
using Sushi.Analysis;
using Sushi.Diagnostics;

namespace Sushi.Configuration;

public sealed record ProjectConfigurationBindResult(ProjectDefinition? Project, IReadOnlyList<SushiDiagnostic> Diagnostics);