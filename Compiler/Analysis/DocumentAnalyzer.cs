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

        return extension switch
        {
            var x when x.Equals(".sus", StringComparison.OrdinalIgnoreCase) => this.sourceAnalyzer.Analyze(snapshot, cancellationToken),
            var x when x.Equals(".susproj", StringComparison.OrdinalIgnoreCase) => this.projectAnalyzer.Analyze(snapshot, cancellationToken),
            var x when x.Equals(".susln", StringComparison.OrdinalIgnoreCase) => this.solutionAnalyzer.Analyze(snapshot, cancellationToken),
            _ => throw new ArgumentException($"Unsupported Sushi document type \"{extension}\".", nameof(snapshot))
        };
    }
}