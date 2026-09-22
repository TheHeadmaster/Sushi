using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a solution source snapshot.
/// </summary>
public sealed class SolutionAnalyzer
{
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new AnalysisResult(snapshot, []));
    }
}