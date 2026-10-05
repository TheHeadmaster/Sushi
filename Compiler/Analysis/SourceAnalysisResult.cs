using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents lexical and syntactic analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Diagnostics">
/// The combined source-encoding, lexical, and syntactic diagnostics.
/// </param>
/// <param name="SyntaxTree">
/// The concrete syntax tree produced from the analyzed source.
/// </param>
public record SourceAnalysisResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics, ConcreteSyntaxTree SyntaxTree) : AnalysisResult(Snapshot, Diagnostics);