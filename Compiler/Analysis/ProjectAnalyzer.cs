using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a project source snapshot.
/// </summary>
public sealed class ProjectAnalyzer : Analyzer
{
    private readonly TomlConfigurationParser parser = new();
    private readonly ProjectConfigurationBinder binder = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        if (!snapshot.IsValidUtf8)
        {
            AccumulateEncodingDiagnostics(diagnosticReporter, snapshot);
            return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, null));
        }

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationParseResult parseResult = this.parser.Parse(snapshot, diagnosticReporter, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        bool hasSyntaxErrors = diagnosticReporter.HasErrors();

        ProjectConfigurationBindResult bindResult = this.binder.Bind(snapshot, diagnosticReporter, parseResult.Document, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ProjectDefinition? project = hasSyntaxErrors ? null : bindResult.Project;

        return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, project));
    }
}