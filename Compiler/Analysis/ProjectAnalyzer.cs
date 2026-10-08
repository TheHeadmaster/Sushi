using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a project source snapshot.
/// </summary>
public sealed class ProjectAnalyzer : Analyzer
{
    public static Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

        if (!snapshot.IsValidUtf8)
        {
            AccumulateEncodingDiagnostics(localDiagnostics, snapshot);
            diagnosticReporter.CommitReporter(localDiagnostics);
            return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, null));
        }

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationParseResult parseResult = TomlConfigurationParser.Parse(snapshot, localDiagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        bool hasSyntaxErrors = localDiagnostics.HasErrors();

        ProjectConfigurationBindResult bindResult = ProjectConfigurationBinder.Bind(snapshot, localDiagnostics, parseResult.Document, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ProjectDefinition? project = hasSyntaxErrors ? null : bindResult.Project;

        diagnosticReporter.CommitReporter(localDiagnostics);

        return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, project));
    }
}