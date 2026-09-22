using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents the compielr analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Diagnostics">
/// The diagnostics produced by the analysis.
/// </param>
public sealed record AnalysisResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics);