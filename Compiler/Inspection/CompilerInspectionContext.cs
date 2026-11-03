using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Retains the exact source and configuration inputs of one inspection without a mutable global registry.
/// Snapshot objects, rather than version numbers alone, preserve authoritative content identity.
/// </summary>
public sealed class CompilerInspectionContext
{
    /// <summary>
    /// Gets the resolved project file identity.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets the authoritative project configuration bytes used by this inspection.
    /// </summary>
    public SourceSnapshot ProjectSnapshot { get; }

    /// <summary>
    /// Gets the analyzed source snapshots in deterministic project discovery order.
    /// </summary>
    public IReadOnlyList<SourceSnapshot> Sources { get; }

    /// <summary>
    /// Gets compiler-generated callable symbols mapped to their defining UTF-8 declaration spans.
    /// Referenced function representations are generated only on a future explicit request.
    /// </summary>
    public IReadOnlyDictionary<string, SourceSpan> Functions { get; }

    /// <summary>
    /// Gets immutable source-level callable signatures keyed by their identities in this context.
    /// </summary>
    public IReadOnlyDictionary<string, InspectionCallableSignature> Callables { get; }

    /// <summary>
    /// Gets exact source-token spans for function and parameter identities in this context.
    /// </summary>
    public IReadOnlyDictionary<InspectionSymbolIdentity, SourceSpan> SourceDefinitions { get; }

    /// <summary>
    /// Freezes the collections describing the completed analysis context.
    /// </summary>
    internal CompilerInspectionContext(string projectPath, SourceSnapshot projectSnapshot,
        IReadOnlyList<SourceSnapshot> sources, IDictionary<string, SourceSpan> functions,
        IDictionary<string, InspectionCallableSignature> callables,
        IDictionary<InspectionSymbolIdentity, SourceSpan> sourceDefinitions)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(projectSnapshot);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(callables);
        ArgumentNullException.ThrowIfNull(sourceDefinitions);

        SourceSnapshot[] sourceArray = [.. sources];
        Dictionary<string, SourceSpan> functionMap = new(functions, StringComparer.Ordinal);
        Dictionary<string, InspectionCallableSignature> callableMap = new(callables, StringComparer.Ordinal);
        Dictionary<InspectionSymbolIdentity, SourceSpan> sourceDefinitionMap = new(sourceDefinitions);

        ValidateFunctionCatalog(sourceArray, functionMap, callableMap);
        ValidateSourceDefinitions(sourceArray, functionMap, callableMap, sourceDefinitionMap);

        this.ProjectPath = projectPath;
        this.ProjectSnapshot = projectSnapshot;
        this.Sources = Array.AsReadOnly(sourceArray);
        this.Functions = new ReadOnlyDictionary<string, SourceSpan>(functionMap);
        this.Callables = new ReadOnlyDictionary<string, InspectionCallableSignature>(callableMap);
        this.SourceDefinitions = new ReadOnlyDictionary<InspectionSymbolIdentity, SourceSpan>(sourceDefinitionMap);
    }

    private static void ValidateFunctionCatalog(SourceSnapshot[] sources,
        IReadOnlyDictionary<string, SourceSpan> functions,
        IReadOnlyDictionary<string, InspectionCallableSignature> callables)
    {
        if (functions.Count != callables.Count)
        {
            throw new ArgumentException("Function declarations and callable signatures must describe the same captured catalog.", nameof(callables));
        }

        foreach (KeyValuePair<string, SourceSpan> item in functions)
        {
            if (!callables.TryGetValue(item.Key, out InspectionCallableSignature signature)
                || !StringComparer.Ordinal.Equals(item.Key, signature.FunctionSymbol)
                || !IsOwnedNonEmptySpan(sources, item.Value))
            {
                throw new ArgumentException("The callable catalog contains an invalid or foreign source declaration.", nameof(functions));
            }
        }
    }

    private static void ValidateSourceDefinitions(SourceSnapshot[] sources,
        IReadOnlyDictionary<string, SourceSpan> functions,
        IReadOnlyDictionary<string, InspectionCallableSignature> callables,
        IReadOnlyDictionary<InspectionSymbolIdentity, SourceSpan> sourceDefinitions)
    {
        foreach (KeyValuePair<InspectionSymbolIdentity, SourceSpan> item in sourceDefinitions)
        {
            InspectionSymbolIdentity identity = item.Key;
            if (identity.Kind is not (InspectionSymbolKind.Function or InspectionSymbolKind.Parameter)
                || !functions.TryGetValue(identity.FunctionSymbol, out SourceSpan functionSpan)
                || !callables.TryGetValue(identity.FunctionSymbol, out InspectionCallableSignature signature)
                || !IsOwnedNonEmptySpan(sources, item.Value)
                || !ReferenceEquals(item.Value.Snapshot, functionSpan.Snapshot)
                || item.Value.Start < functionSpan.Start || item.Value.End > functionSpan.End)
            {
                throw new ArgumentException("A source definition must identify a function or parameter token in its owning declaration.", nameof(sourceDefinitions));
            }

            if (identity.Kind is InspectionSymbolKind.Function && item.Value.Length == 0
                || identity.Kind is InspectionSymbolKind.Parameter
                    && (identity.Index is not int index || index >= signature.Parameters.Count))
            {
                throw new ArgumentException("A source definition does not match its callable signature identity.", nameof(sourceDefinitions));
            }
        }

        foreach (KeyValuePair<string, SourceSpan> function in functions)
        {
            if (!sourceDefinitions.ContainsKey(new InspectionSymbolIdentity(InspectionSymbolKind.Function, function.Key)))
            {
                throw new ArgumentException("Each retained function requires its source name token mapping.", nameof(sourceDefinitions));
            }

            InspectionCallableSignature signature = callables[function.Key];
            for (int index = 0; index < signature.Parameters.Count; index++)
            {
                InspectionSymbolIdentity identity = new(InspectionSymbolKind.Parameter, function.Key, index);
                if (!sourceDefinitions.ContainsKey(identity))
                {
                    throw new ArgumentException("Each retained parameter requires its source name token mapping.", nameof(sourceDefinitions));
                }
            }
        }
    }

    private static bool IsOwnedNonEmptySpan(SourceSnapshot[] sources, SourceSpan span)
        => span.Start >= 0 && span.Start < span.End && span.End <= span.Snapshot.SourceLength
            && sources.Any(snapshot => ReferenceEquals(snapshot, span.Snapshot));
}
