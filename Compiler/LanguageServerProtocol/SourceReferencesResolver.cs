using Sushi.Analysis;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Collects current source occurrences that resolve to one compiler-established declaration.
/// </summary>
internal static class SourceReferencesResolver
{
    /// <summary>
    /// Resolves and deterministically collects references from the selected source's semantic context.
    /// </summary>
    /// <param name="analyses">
    /// All analyses captured from one coherent workspace generation.
    /// </param>
    /// <param name="selectedAnalysis">
    /// The analysis containing the user's cursor position.
    /// </param>
    /// <param name="offset">
    /// The cursor offset in canonical UTF-8 bytes.
    /// </param>
    /// <param name="includeDeclaration">
    /// Whether the symbol's declaration token belongs in the result.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels selection, collection, ordering, or validation.
    /// </param>
    /// <returns>
    /// Unique valid token spans ordered by normalized source path and source position.
    /// </returns>
    internal static IReadOnlyList<SourceSpan> Resolve(
        IReadOnlyList<SourceAnalysisResult> analyses,
        SourceAnalysisResult selectedAnalysis,
        int offset,
        bool includeDeclaration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        ArgumentNullException.ThrowIfNull(selectedAnalysis);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSemanticOccurrence? selected = SourceSemanticOccurrenceLookup.FindOccurrence(
            selectedAnalysis.SemanticOccurrences, selectedAnalysis.Snapshot, offset);
        if (selected is null || !SourceSemanticSymbolIdentity.TryCreate(selected.Symbol, out SourceSemanticSymbolIdentity identity))
        {
            return [];
        }

        List<SourceAnalysisResult> scopedAnalyses = [];
        foreach (SourceAnalysisResult analysis in analyses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedAnalysis.SemanticProjectUri is Uri projectUri
                ? analysis.SemanticProjectUri is Uri candidate && candidate.IsSamePath(projectUri)
                : ReferenceEquals(analysis.Snapshot, selectedAnalysis.Snapshot))
            {
                scopedAnalyses.Add(analysis);
            }
        }

        bool ownsDefinitionSnapshot = false;
        foreach (SourceAnalysisResult analysis in scopedAnalyses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReferenceEquals(analysis.Snapshot, identity.DefinitionSnapshot))
            {
                ownsDefinitionSnapshot = true;
                break;
            }
        }

        SourceSemanticOccurrence definitionOccurrence = new(
            new SourceSpan(identity.DefinitionSnapshot, identity.Start, identity.End), selected.Symbol, IsDeclaration: true);
        if (!ownsDefinitionSnapshot || !IsValidOccurrence(definitionOccurrence, identity.DefinitionSnapshot))
        {
            return [];
        }

        List<SourceSpan> matches = [];
        foreach (SourceAnalysisResult analysis in scopedAnalyses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (SourceSemanticOccurrence occurrence in analysis.SemanticOccurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((!includeDeclaration && occurrence.IsDeclaration)
                    || !identity.Matches(occurrence.Symbol)
                    || !IsValidOccurrence(occurrence, analysis.Snapshot))
                {
                    continue;
                }

                matches.Add(occurrence.Span);
            }
        }

        matches.Sort((left, right) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CompareSpans(left, right);
        });
        List<SourceSpan> unique = new(matches.Count);
        foreach (SourceSpan span in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (unique.Count == 0 || !IsSameLocation(unique[^1], span))
            {
                unique.Add(span);
            }
        }

        return Array.AsReadOnly(unique.ToArray());
    }

    private static bool IsValidOccurrence(SourceSemanticOccurrence occurrence, SourceSnapshot snapshot)
    {
        SourceSpan span = occurrence.Span;
        if (!ReferenceEquals(span.Snapshot, snapshot) || !snapshot.IsValidUtf8 || span.Start < 0
            || span.Start >= span.End || span.End > snapshot.SourceLength)
        {
            return false;
        }

        try
        {
            (int startLine, int startCharacter) = snapshot.GetUtf16Position(span.Start);
            (int endLine, int endCharacter) = snapshot.GetUtf16Position(span.End);
            return snapshot.GetByteOffset(startLine, startCharacter) == span.Start
                && snapshot.GetByteOffset(endLine, endCharacter) == span.End;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static int CompareSpans(SourceSpan left, SourceSpan right)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        int pathComparison = string.Compare(Path.GetFullPath(left.Snapshot.Uri.LocalPath),
            Path.GetFullPath(right.Snapshot.Uri.LocalPath), comparison);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        int startComparison = left.Start.CompareTo(right.Start);
        return startComparison != 0 ? startComparison : left.End.CompareTo(right.End);
    }

    private static bool IsSameLocation(SourceSpan left, SourceSpan right)
        => left.Snapshot.Uri.IsSamePath(right.Snapshot.Uri) && left.Start == right.Start && left.End == right.End;
}
