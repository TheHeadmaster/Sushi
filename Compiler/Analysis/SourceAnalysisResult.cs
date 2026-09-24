using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents the compiler source code analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Diagnostics">
/// The diagnostics produced by the analysis.
/// </param>
public record SourceAnalysisResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics) : AnalysisResult(Snapshot, Diagnostics);