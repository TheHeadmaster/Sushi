using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a project source snapshot.
/// </summary>
public sealed class ProjectAnalyzer
{
    private readonly TomlConfigurationParser parser = new();
    private readonly ProjectConfigurationBinder binder = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationParseResult parseResult = this.parser.Parse(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ProjectConfigurationBindResult bindResult = this.binder.Bind(snapshot, parseResult.Syntax, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SushiDiagnostic[] diagnostics =
        [
            .. parseResult.Diagnostics,
            .. bindResult.Diagnostics
        ];

        bool hasSyntaxErrors = parseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        ProjectDefinition? project = hasSyntaxErrors ? null : bindResult.Project;

        return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, diagnostics, project));
    }
}