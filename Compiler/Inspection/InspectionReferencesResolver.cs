namespace Sushi.Inspection;

/// <summary>
/// Resolves recorded references to the semantic symbol under a cursor in one generated representation.
/// </summary>
public static class InspectionReferencesResolver
{
    /// <summary>
    /// Returns source-ordered occurrences with the selected occurrence's complete semantic identity.
    /// </summary>
    /// <param name="document">
    /// The immutable generated representation that bounds the search.
    /// </param>
    /// <param name="offset">
    /// The zero-based UTF-16 cursor offset in generated text.
    /// </param>
    /// <param name="includeDeclaration">
    /// Whether definition occurrences should be included with references.
    /// </param>
    /// <returns>
    /// Recorded occurrences from this document, or an empty list when no semantic symbol is selected.
    /// </returns>
    public static IReadOnlyList<InspectionSymbolOccurrence> Resolve(InspectionDocument document, int offset, bool includeDeclaration)
    {
        ArgumentNullException.ThrowIfNull(document);
        InspectionSymbolOccurrence? selected = InspectionDefinitionResolver.FindOccurrence(document, offset);
        if (selected is not InspectionSymbolOccurrence origin)
        {
            return Array.Empty<InspectionSymbolOccurrence>();
        }

        InspectionSymbolOccurrence[] matches = [.. document.Occurrences.Where(occurrence =>
            occurrence.Identity == origin.Identity && (includeDeclaration || !occurrence.IsDefinition))];
        return Array.AsReadOnly(matches);
    }
}
