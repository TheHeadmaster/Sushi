using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a solution source snapshot.
/// </summary>
public sealed class SolutionAnalyzer : Analyzer
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
            return Task.FromResult<AnalysisResult>(new SolutionAnalysisResult(snapshot, null));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // TODO: This result has a use later
        TomlConfigurationParseResult _ = TomlConfigurationParser.Parse(snapshot, localDiagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        diagnosticReporter.CommitReporter(localDiagnostics);

        return Task.FromResult<AnalysisResult>(new SolutionAnalysisResult(snapshot, new SolutionDefinition()));
    }
}