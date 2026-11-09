
using Sushi.Documentation;
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
/// <param name="SyntaxTree">
/// The concrete syntax tree produced from the analyzed source.
/// </param>
/// <param name="Package">
/// The semantic identity of the declared package when the declaration can be bound reliably.
/// </param>
/// <param name="Namespace">
/// The semantic identity of the declared namespace when the declaration can be bound reliably.
/// </param>
/// <param name="Functions">
/// The bound package-level functions.
/// </param>
public record SourceAnalysisResult(SourceSnapshot Snapshot, ConcreteSyntaxTree SyntaxTree, PackageIdentity? Package, NamespaceIdentity? Namespace, IReadOnlyList<BoundFunction> Functions) : AnalysisResult(Snapshot)
{
    /// <summary>
    /// Gets the successfully bound activity declarations in this source file.
    /// </summary>
    public IReadOnlyList<BoundActivity> Activities { get; init; } = [];

    /// <summary>
    /// Gets the source-backed documentation blocks associated with the analyzed syntax tree.
    /// </summary>
    public IReadOnlyList<DocumentationBlock> DocumentationBlocks { get; init; } = [];

    /// <summary>
    /// Gets source-token occurrences resolved by the compiler binding path.
    /// </summary>
    internal IReadOnlyList<SourceSemanticOccurrence> SemanticOccurrences { get; init; } = [];

    /// <summary>
    /// Gets compiler-resolved argument-to-parameter mappings captured for this source snapshot.
    /// </summary>
    internal IReadOnlyList<BoundParameterHint> ParameterHints { get; init; } = [];

    /// <summary>
    /// Gets callable signatures visible from this source under the compiler's
    /// package, namespace, and accessibility rules.
    /// This metadata remains available when body binding cannot complete.
    /// </summary>
    internal IReadOnlyList<PackageCallable> VisibleCallables { get; init; } = [];

    /// <summary>
    /// Gets the project whose callable catalog resolved this file, or null when it was analyzed as a standalone source.
    /// </summary>
    internal Uri? SemanticProjectUri { get; init; }
}
