using Sushi.Configuration;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a solution source snapshot.
/// </summary>
public sealed class SolutionAnalyzer
{
    private readonly TomlConfigurationParser parser = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationParseResult parseResult = this.parser.Parse(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<AnalysisResult>(new SolutionAnalysisResult(snapshot, parseResult.Diagnostics, new SolutionDefinition()));
    }
}