using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a general document source snapshot.
/// </summary>
public sealed class DocumentAnalyzer : Analyzer
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        string extension = Path.GetExtension(snapshot.Uri.LocalPath);

        return extension switch
        {
            var x when x.Equals(".sus", StringComparison.OrdinalIgnoreCase) => this.sourceAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            var x when x.Equals(".susproj", StringComparison.OrdinalIgnoreCase) => ProjectAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            var x when x.Equals(".susln", StringComparison.OrdinalIgnoreCase) => SolutionAnalyzer.Analyze(snapshot, diagnosticReporter, cancellationToken),
            _ => throw new ArgumentException($"Unsupported Sushi document type \"{extension}\".", nameof(snapshot))
        };
    }
}