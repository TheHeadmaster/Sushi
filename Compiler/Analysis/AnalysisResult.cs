using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents the compiler analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
public abstract record AnalysisResult(SourceSnapshot Snapshot);