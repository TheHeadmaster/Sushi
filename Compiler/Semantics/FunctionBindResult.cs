using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Contains a successfully bound function and the source tokens that declared its locals.
/// </summary>
public sealed record FunctionBindResult
{
    /// <summary>
    /// Gets the bound function when the declaration can be represented by the current semantic slice.
    /// </summary>
    public BoundFunction? Function { get; }

    /// <summary>
    /// Gets source-name spans keyed by the function-unique local indices assigned during binding.
    /// </summary>
    public IReadOnlyDictionary<int, SourceSpan> LocalDefinitions { get; }

    /// <summary>
    /// Gets exact source occurrences resolved while binding the function body.
    /// </summary>
    internal IReadOnlyList<SourceSemanticOccurrence> SemanticOccurrences { get; }

    /// <summary>
    /// Gets compiler-resolved invocation argument mappings captured during body binding.
    /// </summary>
    internal IReadOnlyList<BoundParameterHint> SemanticParameterHints { get; }

    /// <summary>
    /// Creates a binding result without local declaration provenance.
    /// </summary>
    /// <param name="function">
    /// The bound function, or null when binding did not succeed.
    /// </param>
    public FunctionBindResult(BoundFunction? function)
        : this(function, new Dictionary<int, SourceSpan>(), [], [])
    {
    }

    /// <summary>
    /// Creates a binding result with immutable local declaration provenance.
    /// </summary>
    /// <param name="function">
    /// The bound function, or null when binding did not succeed.
    /// </param>
    /// <param name="localDefinitions">
    /// Source-name spans captured as the binder declared each function local.
    /// </param>
    internal FunctionBindResult(BoundFunction? function, IReadOnlyDictionary<int, SourceSpan> localDefinitions, IReadOnlyList<SourceSemanticOccurrence>? semanticOccurrences = null, IReadOnlyList<BoundParameterHint>? semanticParameterHints = null)
    {
        ArgumentNullException.ThrowIfNull(localDefinitions);
        ArgumentNullException.ThrowIfNull(semanticOccurrences);
        this.Function = function;
        this.LocalDefinitions = new ReadOnlyDictionary<int, SourceSpan>(localDefinitions.ToDictionary(item => item.Key, item => item.Value));
        this.SemanticOccurrences = Array.AsReadOnly(semanticOccurrences.ToArray());
        this.SemanticParameterHints = Array.AsReadOnly((semanticParameterHints ?? []).ToArray());
    }

    /// <summary>
    /// Deconstructs the result using the original function-only binding contract.
    /// </summary>
    /// <param name="function">
    /// The bound function, or null when binding did not succeed.
    /// </param>
    public void Deconstruct(out BoundFunction? function) => function = this.Function;
}
