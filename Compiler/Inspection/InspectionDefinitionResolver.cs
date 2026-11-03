namespace Sushi.Inspection;

/// <summary>
/// Resolves a generated-text position to a definition recorded by its formatter.
/// </summary>
public static class InspectionDefinitionResolver
{
    /// <summary>
    /// Finds the symbol occurrence containing a UTF-16 position and the exact matching definition.
    /// </summary>
    /// <param name="document">The immutable generated representation.</param>
    /// <param name="offset">The zero-based UTF-16 cursor offset.</param>
    /// <returns>The reference and its definition, or null when no definition is recorded.</returns>
    public static (InspectionSymbolOccurrence Origin, InspectionSymbolOccurrence Definition)? Resolve(InspectionDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        InspectionSymbolOccurrence? found = FindOccurrence(document, offset);
        if (found is not InspectionSymbolOccurrence occurrence)
        {
            return null;
        }

        if (occurrence.IsDefinition)
        {
            return null;
        }

        InspectionSymbolOccurrence? definition = document.Occurrences.FirstOrDefault(item => item.IsDefinition && item.Identity == occurrence.Identity);
        return definition is InspectionSymbolOccurrence target && target.IsDefinition ? (occurrence, target) : null;
    }

    /// <summary>
    /// Finds the semantic occurrence under a UTF-16 position without requiring a definition in this artifact.
    /// </summary>
    /// <param name="document">The immutable generated representation.</param>
    /// <param name="offset">The zero-based UTF-16 cursor offset.</param>
    /// <returns>The recorded occurrence under the position, or null when it is outside a symbol range.</returns>
    public static InspectionSymbolOccurrence? FindOccurrence(InspectionDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (offset < 0 || offset >= document.Text.Length)
        {
            return null;
        }

        InspectionSymbolOccurrence? origin = null;
        foreach (InspectionSymbolOccurrence candidate in document.Occurrences)
        {
            if (offset >= candidate.Start && offset < candidate.End)
            {
                origin = candidate;
                break;
            }
        }

        if (origin is not InspectionSymbolOccurrence occurrence || occurrence.Identity.Kind is not (
            InspectionSymbolKind.Function or InspectionSymbolKind.Parameter or InspectionSymbolKind.Local
            or InspectionSymbolKind.BasicBlock or InspectionSymbolKind.Temporary))
        {
            return null;
        }
        return occurrence;
    }
}