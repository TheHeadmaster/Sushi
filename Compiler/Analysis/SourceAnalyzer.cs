using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a source snapshot.
/// </summary>
public sealed class SourceAnalyzer
{
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new AnalysisResult(snapshot, []));
    }
}