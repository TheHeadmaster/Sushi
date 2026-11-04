using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Finds compiler-resolved source occurrences in their owning source snapshot.
/// </summary>
internal static class SourceSemanticOccurrenceLookup
{
    /// <summary>
    /// Returns the occurrence covering a canonical source byte offset.
    /// </summary>
    /// <param name="occurrences">Source occurrences ordered by their source spans.</param>
    /// <param name="snapshot">The authoritative snapshot whose occurrence is being selected.</param>
    /// <param name="offset">The UTF-8 byte offset to resolve.</param>
    /// <returns>The matching half-open occurrence, or null when no symbol covers the offset.</returns>
    internal static SourceSemanticOccurrence? FindOccurrence(
        IReadOnlyList<SourceSemanticOccurrence> occurrences,
        SourceSnapshot snapshot,
        int offset)
    {
        int low = 0;
        int high = occurrences.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (occurrences[middle].Span.Start <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int candidate = low - 1;
        if (candidate < 0)
        {
            return null;
        }

        SourceSemanticOccurrence occurrence = occurrences[candidate];
        return ReferenceEquals(occurrence.Span.Snapshot, snapshot) && offset < occurrence.Span.End ? occurrence : null;
    }
}
