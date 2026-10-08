using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents the compiler solution analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Solution">
/// The solution definition produced by the analysis.
/// </param>
public record SolutionAnalysisResult(SourceSnapshot Snapshot, SolutionDefinition? Solution) : AnalysisResult(Snapshot);