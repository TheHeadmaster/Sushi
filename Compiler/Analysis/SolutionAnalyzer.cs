using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a solution source snapshot.
/// </summary>
public sealed class SolutionAnalyzer : Analyzer
{
    private readonly TomlConfigurationParser parser = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        if (!snapshot.IsValidUtf8)
        {
            AccumulateEncodingDiagnostics(diagnosticReporter, snapshot);
            return Task.FromResult<AnalysisResult>(new SolutionAnalysisResult(snapshot, null));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // TODO: This result has a use later
        TomlConfigurationParseResult _ = this.parser.Parse(snapshot, diagnosticReporter, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<AnalysisResult>(new SolutionAnalysisResult(snapshot, new SolutionDefinition()));
    }
}