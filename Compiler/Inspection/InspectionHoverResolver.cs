using Sushi.Semantics;

namespace Sushi.Inspection;

/// <summary>
/// Resolves typed hover information from formatter metadata and the captured callable catalog.
/// </summary>
public static class InspectionHoverResolver
{
    /// <summary>
    /// Finds the semantic symbol at a generated-text UTF-16 position and describes it without compilation.
    /// </summary>
    /// <param name="document">
    /// The immutable representation whose formatter metadata is queried.
    /// </param>
    /// <param name="offset">
    /// The zero-based UTF-16 offset in the generated text.
    /// </param>
    /// <param name="context">
    /// The exact source-level callable catalog retained by this inspection.
    /// </param>
    /// <returns>
    /// A typed hover description, or null when the position or metadata is unavailable.
    /// </returns>
    public static InspectionHoverResult? Resolve(InspectionDocument document, int offset, CompilerInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        InspectionSymbolOccurrence? occurrence = InspectionDefinitionResolver.FindOccurrence(document, offset);
        if (occurrence is not InspectionSymbolOccurrence symbol
            || !context.Callables.TryGetValue(symbol.Identity.FunctionSymbol, out InspectionCallableSignature? callable))
        {
            return null;
        }

        InspectionParameterSignature? parameter = null;
        if (symbol.Identity.Kind is InspectionSymbolKind.Parameter)
        {
            int index = symbol.Identity.Index ?? -1;
            if (index < 0 || index >= callable.Parameters.Count || callable.Parameters[index].Index != index)
            {
                return null;
            }

            parameter = callable.Parameters[index];
        }

        InspectionSymbolMetadata? metadata = null;
        if (symbol.Identity.Kind is (InspectionSymbolKind.Local or InspectionSymbolKind.BasicBlock or InspectionSymbolKind.Temporary)
            && !document.SymbolMetadata.TryGetValue(symbol.Identity, out metadata))
        {
            return null;
        }

        if (symbol.Identity.Kind is not (InspectionSymbolKind.Function or InspectionSymbolKind.Parameter
            or InspectionSymbolKind.Local or InspectionSymbolKind.BasicBlock or InspectionSymbolKind.Temporary))
        {
            return null;
        }

        return new InspectionHoverResult(symbol, callable, parameter, metadata);
    }
}

/// <summary>
/// Carries the semantic source signature and formatter descriptor associated with one hovered occurrence.
/// </summary>
/// <param name="Occurrence">
/// The exact generated-text occurrence under the cursor.
/// </param>
/// <param name="Callable">
/// The owning function signature in the captured compilation context.
/// </param>
/// <param name="Parameter">
/// The indexed source parameter for parameter symbols.
/// </param>
/// <param name="Metadata">
/// The typed formatter descriptor for local, temporary, or block symbols.
/// </param>
public sealed record InspectionHoverResult(
    InspectionSymbolOccurrence Occurrence,
    InspectionCallableSignature Callable,
    InspectionParameterSignature? Parameter,
    InspectionSymbolMetadata? Metadata);
