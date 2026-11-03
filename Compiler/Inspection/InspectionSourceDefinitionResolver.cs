using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Resolves supported generated symbols to source tokens in their owning inspection snapshot.
/// </summary>
public static class InspectionSourceDefinitionResolver
{
    /// <summary>
    /// Finds the source definition associated with a generated-text occurrence.
    /// </summary>
    /// <param name="document">
    /// The immutable generated IR document.
    /// </param>
    /// <param name="offset">
    /// The zero-based UTF-16 position in generated text.
    /// </param>
    /// <param name="context">
    /// The compilation context that owns function and parameter identities and source snapshots.
    /// </param>
    /// <param name="localDefinitions">
    /// The selected function's per-artifact local declaration tokens, captured when its body was bound.
    /// </param>
    /// <returns>
    /// The generated occurrence and its exact source-token span, or null when that symbol has no source mapping.
    /// </returns>
    public static InspectionSourceDefinition? Resolve(InspectionDocument document, int offset, CompilerInspectionContext context,
        IReadOnlyDictionary<int, SourceSpan>? localDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        InspectionSymbolOccurrence? occurrence = InspectionDefinitionResolver.FindOccurrence(document, offset);
        if (occurrence is not InspectionSymbolOccurrence symbol)
        {
            return null;
        }

        SourceSpan sourceSpan;
        if (symbol.Identity.Kind is InspectionSymbolKind.Local)
        {
            if (!StringComparer.Ordinal.Equals(symbol.Identity.FunctionSymbol, document.FunctionSymbol)
                || symbol.Identity.Index is not int localIndex || localDefinitions is null
                || !localDefinitions.TryGetValue(localIndex, out sourceSpan))
            {
                return null;
            }
        }
        else if (symbol.Identity.Kind is InspectionSymbolKind.Function or InspectionSymbolKind.Parameter)
        {
            if (!context.SourceDefinitions.TryGetValue(symbol.Identity, out sourceSpan))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        return new InspectionSourceDefinition(symbol, sourceSpan);
    }
}

/// <summary>
/// Associates one generated symbol occurrence with its retained source declaration token.
/// </summary>
/// <param name="Occurrence">
/// The exact generated-text range under the cursor.
/// </param>
/// <param name="SourceSpan">
/// The authoritative source-token range in a captured source snapshot.
/// </param>
public sealed record InspectionSourceDefinition(InspectionSymbolOccurrence Occurrence, SourceSpan SourceSpan);
