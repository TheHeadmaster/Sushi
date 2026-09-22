using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a project source snapshot.
/// </summary>
public sealed class ProjectAnalyzer
{
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new AnalysisResult(snapshot, []));
    }
}