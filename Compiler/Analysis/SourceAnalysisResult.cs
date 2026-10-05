using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Represents lexical, syntactic, and initial semantic analysis produced for a specific source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The snapshot that was analyzed.
/// </param>
/// <param name="Diagnostics">
/// The combined source-encoding, lexical, syntactic, and semantic diagnostics.
/// </param>
/// <param name="SyntaxTree">
/// The concrete syntax tree produced from the analyzed source.
/// </param>
/// <param name="Package">
/// The semantic identity of the declared package when the declaration can be bound reliably.
/// </param>
/// <param name="Namespace">
/// The semantic identity of the declared namespace when the declaration can be bound reliably.
/// </param>
public record SourceAnalysisResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics, ConcreteSyntaxTree SyntaxTree, PackageIdentity? Package, NamespaceIdentity? Namespace) : AnalysisResult(Snapshot, Diagnostics);