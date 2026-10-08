using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a general document source snapshot.
/// </summary>
public sealed class DocumentAnalyzer : Analyzer
{
    private readonly SourceAnalyzer sourceAnalyzer = new();
    private readonly ProjectAnalyzer projectAnalyzer = new();
    private readonly SolutionAnalyzer solutionAnalyzer = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        string extension = Path.GetExtension(snapshot.Uri.LocalPath);

        return extension switch
        {
            var x when x.Equals(".sus", StringComparison.OrdinalIgnoreCase) => this.sourceAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            var x when x.Equals(".susproj", StringComparison.OrdinalIgnoreCase) => this.projectAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            var x when x.Equals(".susln", StringComparison.OrdinalIgnoreCase) => this.solutionAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            _ => throw new ArgumentException($"Unsupported Sushi document type \"{extension}\".", nameof(snapshot))
        };
    }
}