using Sushi.Configuration;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a project source snapshot.
/// </summary>
public sealed class ProjectAnalyzer
{
    private readonly TomlConfigurationParser parser = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationParseResult parseResult = this.parser.Parse(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<AnalysisResult>(new ProjectAnalysisResult(snapshot, parseResult.Diagnostics, Project: null));
    }
}