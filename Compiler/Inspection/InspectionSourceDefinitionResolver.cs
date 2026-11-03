using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Resolves function and parameter occurrences to source tokens in their captured compilation context.
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
    /// The compilation context that owns the document's semantic identities and source snapshots.
    /// </param>
    /// <returns>
    /// The generated occurrence and its exact source-token span, or null when that symbol has no source mapping.
    /// </returns>
    public static InspectionSourceDefinition? Resolve(InspectionDocument document, int offset, CompilerInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        InspectionSymbolOccurrence? occurrence = InspectionDefinitionResolver.FindOccurrence(document, offset);
        if (occurrence is not InspectionSymbolOccurrence symbol
            || symbol.Identity.Kind is not (InspectionSymbolKind.Function or InspectionSymbolKind.Parameter)
            || !context.SourceDefinitions.TryGetValue(symbol.Identity, out SourceSpan sourceSpan))
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
