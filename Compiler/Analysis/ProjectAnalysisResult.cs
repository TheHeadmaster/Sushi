using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents the compiler project analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Diagnostics">
/// The diagnostics produced by the analysis.
/// </param>
/// <param name="Project">
/// The project definition produced by the analysis.
/// </param>
public record ProjectAnalysisResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics, ProjectDefinition? Project) : AnalysisResult(Snapshot, Diagnostics);