using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a general document source snapshot.
/// </summary>
public sealed class DocumentAnalyzer
{
    private readonly SourceAnalyzer sourceAnalyzer = new();
    private readonly ProjectAnalyzer projectAnalyzer = new();
    private readonly SolutionAnalyzer solutionAnalyzer = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string extension = Path.GetExtension(snapshot.Uri.LocalPath);

        return extension.ToLowerInvariant() switch
        {
            ".sus" => this.sourceAnalyzer.Analyze(snapshot, cancellationToken),
            ".susproj" => this.projectAnalyzer.Analyze(snapshot, cancellationToken),
            ".susln" => this.solutionAnalyzer.Analyze(snapshot, cancellationToken),
            _ => throw new ArgumentException($"Unsupported Sushi document type \"{extension}\".", nameof(snapshot))
        };
    }
}