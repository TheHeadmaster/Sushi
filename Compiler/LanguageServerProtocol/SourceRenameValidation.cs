using System.Text;
using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Holds a source-backed text replacement used by a prospective rename analysis.
/// </summary>
/// <param name="Span">The exact token span in the captured source.</param>
/// <param name="NewText">The replacement spelling.</param>
internal sealed record SourceRenameTextEdit(SourceSpan Span, string NewText);

/// <summary>
/// Validates rename edits by analyzing immutable candidate snapshots before the server returns them.
/// </summary>
internal static class SourceRenameValidation
{
    /// <summary>
    /// Confirms that a rename preserves every existing semantic occurrence and introduces no new diagnostics.
    /// </summary>
    /// <param name="analysisSet">The coherent source context used to resolve the rename target.</param>
    /// <param name="target">The exact compiler-resolved declaration selected for renaming.</param>
    /// <param name="references">The declaration and references returned by the shared reference resolver.</param>
    /// <param name="edits">All code and structured documentation replacements.</param>
    /// <param name="cancellationToken">Cancels candidate construction and semantic analysis.</param>
    /// <returns>True only when the complete candidate source set retains its prior bindings safely.</returns>
    internal static async Task<bool> IsSemanticallySafeAsync(
        SourceSemanticAnalysisSet analysisSet,
        SourceSemanticOccurrence target,
        IReadOnlyList<SourceSpan> references,
        IReadOnlyList<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysisSet);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(edits);
        cancellationToken.ThrowIfCancellationRequested();

        if (edits.Count == 0 || references.Count == 0 || target.Symbol.DefinitionSpan is not SourceSpan targetDefinition)
        {
            return false;
        }

        if (!TryCreateSnapshotSet(analysisSet, edits, cancellationToken,
            out IReadOnlyList<SourceSnapshot> originalSnapshots, out IReadOnlyList<SourceSnapshot> candidateSnapshots))
        {
            return false;
        }

        ProjectDiagnosticAnalyzer analyzer = new();
        Uri selectedUri = analysisSet.Selected.Snapshot.Uri;
        IReadOnlyList<ProjectDiagnosticResult> baseline = await analyzer.AnalyzeAsync(originalSnapshots, [selectedUri], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ProjectDiagnosticResult> candidate = await analyzer.AnalyzeAsync(candidateSnapshots, [selectedUri], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<SourceAnalysisResult> baselineAnalyses = baseline.Select(result => result.Analysis).OfType<SourceAnalysisResult>().ToArray();
        IReadOnlyList<SourceAnalysisResult> candidateAnalyses = candidate.Select(result => result.Analysis).OfType<SourceAnalysisResult>().ToArray();
        if (!HasSameMappedOccurrences(baselineAnalyses, candidateAnalyses, edits, cancellationToken)
            || IntroducesDiagnostics(baseline, candidate, edits, cancellationToken)
            || !RetainsAllRenamedOccurrences(candidateAnalyses, target.Symbol.Kind, target.Symbol.Index, targetDefinition,
                references, edits, cancellationToken))
        {
            return false;
        }

        return true;
    }

    private static bool TryCreateSnapshotSet(
        SourceSemanticAnalysisSet analysisSet,
        IReadOnlyList<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken,
        out IReadOnlyList<SourceSnapshot> originalSnapshots,
        out IReadOnlyList<SourceSnapshot> candidateSnapshots)
    {
        Dictionary<string, SourceSnapshot> snapshotsByPath = new(PathComparer);
        foreach (SourceSnapshot snapshot in analysisSet.WorkspaceSnapshots.Concat(analysisSet.Analyses.Select(analysis => analysis.Snapshot)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            snapshotsByPath[Path.GetFullPath(snapshot.Uri.LocalPath)] = snapshot;
        }

        List<SourceSnapshot> original = [.. snapshotsByPath.Values];
        Dictionary<string, List<SourceRenameTextEdit>> editsByPath = [];
        foreach (SourceRenameTextEdit edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(edit.Span.Snapshot.Uri.LocalPath);
            if (!ReferenceEquals(snapshotsByPath.GetValueOrDefault(path), edit.Span.Snapshot))
            {
                originalSnapshots = [];
                candidateSnapshots = [];
                return false;
            }

            if (!editsByPath.TryGetValue(path, out List<SourceRenameTextEdit>? pathEdits))
            {
                pathEdits = [];
                editsByPath.Add(path, pathEdits);
            }

            pathEdits.Add(edit);
        }

        List<SourceSnapshot> candidates = [];
        foreach ((string path, SourceSnapshot snapshot) in snapshotsByPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (editsByPath.TryGetValue(path, out List<SourceRenameTextEdit>? pathEdits))
            {
                if (!TryApplyEdits(snapshot, pathEdits, cancellationToken, out byte[] bytes))
                {
                    originalSnapshots = [];
                    candidateSnapshots = [];
                    return false;
                }

                candidates.Add(SourceSnapshot.FromUtf8(snapshot.Uri, snapshot.Version, bytes));
            }
            else
            {
                candidates.Add(snapshot);
            }
        }

        originalSnapshots = Array.AsReadOnly(original.ToArray());
        candidateSnapshots = Array.AsReadOnly(candidates.ToArray());
        return true;
    }

    private static bool TryApplyEdits(SourceSnapshot snapshot, List<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken, out byte[] bytes)
    {
        bytes = [];
        if (!snapshot.IsValidUtf8)
        {
            return false;
        }

        edits.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
        int previousEnd = -1;
        foreach (SourceRenameTextEdit edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(edit.Span.Snapshot, snapshot) || edit.Span.Start < previousEnd
                || edit.Span.Start < 0 || edit.Span.Start >= edit.Span.End || edit.Span.End > snapshot.SourceLength)
            {
                return false;
            }

            previousEnd = edit.Span.End;
        }

        using MemoryStream stream = new();
        int cursor = 0;
        foreach (SourceRenameTextEdit edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Write(snapshot.Bytes.Span[cursor..edit.Span.Start]);
            stream.Write(Encoding.UTF8.GetBytes(edit.NewText));
            cursor = edit.Span.End;
        }

        stream.Write(snapshot.Bytes.Span[cursor..]);
        bytes = stream.ToArray();
        return true;
    }

    private static bool HasSameMappedOccurrences(
        IReadOnlyList<SourceAnalysisResult> baseline,
        IReadOnlyList<SourceAnalysisResult> candidate,
        IReadOnlyList<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken)
    {
        Dictionary<OccurrenceKey, int> expected = [];
        foreach (SourceAnalysisResult analysis in baseline)
        {
            foreach (SourceSemanticOccurrence occurrence in analysis.SemanticOccurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryMapSpan(occurrence.Span, edits, out int start, out int end)
                    || !TryMapDefinition(occurrence.Symbol.DefinitionSpan, edits, out SourceSpan? definition))
                {
                    return false;
                }

                OccurrenceKey key = new(Path.GetFullPath(analysis.Snapshot.Uri.LocalPath), start, end,
                    occurrence.Symbol.Kind,
                    definition?.Start ?? -1, definition?.End ?? -1,
                    definition is SourceSpan target ? Path.GetFullPath(target.Snapshot.Uri.LocalPath) : string.Empty);
                expected.TryGetValue(key, out int count);
                expected[key] = count + 1;
            }
        }

        Dictionary<OccurrenceKey, int> actual = [];
        foreach (SourceAnalysisResult analysis in candidate)
        {
            foreach (SourceSemanticOccurrence occurrence in analysis.SemanticOccurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SourceSpan? definition = occurrence.Symbol.DefinitionSpan;
                OccurrenceKey key = new(Path.GetFullPath(analysis.Snapshot.Uri.LocalPath), occurrence.Span.Start, occurrence.Span.End,
                    occurrence.Symbol.Kind,
                    definition?.Start ?? -1, definition?.End ?? -1,
                    definition is SourceSpan target ? Path.GetFullPath(target.Snapshot.Uri.LocalPath) : string.Empty);
                actual.TryGetValue(key, out int count);
                actual[key] = count + 1;
            }
        }

        return expected.Count == actual.Count && expected.All(item => actual.TryGetValue(item.Key, out int count) && count == item.Value);
    }

    private static bool IntroducesDiagnostics(
        IReadOnlyList<ProjectDiagnosticResult> baseline,
        IReadOnlyList<ProjectDiagnosticResult> candidate,
        IReadOnlyList<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken)
    {
        Dictionary<DiagnosticKey, int> expected = [];
        foreach (ProjectDiagnosticResult result in baseline)
        {
            foreach (SushiDiagnostic diagnostic in result.Reporter.ReportDiagnostics())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryMapSpan(diagnostic.Span, edits, out int start, out int end))
                {
                    return false;
                }

                DiagnosticKey key = new(Path.GetFullPath(diagnostic.Span.Snapshot.Uri.LocalPath), diagnostic.Code,
                    diagnostic.Severity, start, end);
                expected.TryGetValue(key, out int count);
                expected[key] = count + 1;
            }
        }

        Dictionary<DiagnosticKey, int> actual = [];
        foreach (ProjectDiagnosticResult result in candidate)
        {
            foreach (SushiDiagnostic diagnostic in result.Reporter.ReportDiagnostics())
            {
                cancellationToken.ThrowIfCancellationRequested();
                DiagnosticKey key = new(Path.GetFullPath(diagnostic.Span.Snapshot.Uri.LocalPath), diagnostic.Code,
                    diagnostic.Severity, diagnostic.Span.Start, diagnostic.Span.End);
                actual.TryGetValue(key, out int count);
                actual[key] = count + 1;
            }
        }

        return actual.All(item => expected.TryGetValue(item.Key, out int count) && item.Value <= count);
    }

    private static bool RetainsAllRenamedOccurrences(
        IReadOnlyList<SourceAnalysisResult> candidates,
        SourceSemanticSymbolKind kind,
        int index,
        SourceSpan definition,
        IReadOnlyList<SourceSpan> references,
        IReadOnlyList<SourceRenameTextEdit> edits,
        CancellationToken cancellationToken)
    {
        if (!TryMapSpan(definition, edits, out int definitionStart, out int definitionEnd))
        {
            return false;
        }

        HashSet<SourceLocationKey> expected = [];
        foreach (SourceSpan span in references)
        {
            if (!TryMapSpan(span, edits, out int start, out int end))
            {
                return false;
            }

            expected.Add(new SourceLocationKey(Path.GetFullPath(span.Snapshot.Uri.LocalPath), start, end));
        }

        HashSet<SourceLocationKey> actual = [];
        foreach (SourceAnalysisResult analysis in candidates)
        {
            foreach (SourceSemanticOccurrence occurrence in analysis.SemanticOccurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (occurrence.Symbol.Kind == kind
                    && occurrence.Symbol.DefinitionSpan is SourceSpan candidateDefinition
                    && Path.GetFullPath(candidateDefinition.Snapshot.Uri.LocalPath) == Path.GetFullPath(definition.Snapshot.Uri.LocalPath)
                    && candidateDefinition.Start == definitionStart && candidateDefinition.End == definitionEnd)
                {
                    actual.Add(new SourceLocationKey(Path.GetFullPath(analysis.Snapshot.Uri.LocalPath), occurrence.Span.Start, occurrence.Span.End));
                }
            }
        }

        return expected.SetEquals(actual);
    }

    private static bool TryMapDefinition(SourceSpan? definition, IReadOnlyList<SourceRenameTextEdit> edits, out SourceSpan? mapped)
    {
        mapped = null;
        if (definition is not SourceSpan span)
        {
            return true;
        }

        if (!TryMapSpan(span, edits, out int start, out int end))
        {
            return false;
        }

        mapped = new SourceSpan(span.Snapshot, start, end);
        return true;
    }

    internal static bool TryMapSpan(SourceSpan span, IReadOnlyList<SourceRenameTextEdit> edits, out int start, out int end)
    {
        int startDelta = 0;
        int containedDelta = 0;
        SourceRenameTextEdit? exactEdit = null;
        foreach (SourceRenameTextEdit edit in edits)
        {
            if (!edit.Span.Snapshot.Uri.IsSamePath(span.Snapshot.Uri))
            {
                continue;
            }

            if (span.Start == edit.Span.Start && span.End == edit.Span.End)
            {
                exactEdit = edit;
                continue;
            }

            int editDelta = Encoding.UTF8.GetByteCount(edit.NewText) - edit.Span.Length;
            if (edit.Span.End <= span.Start)
            {
                startDelta += editDelta;
            }
            else if (edit.Span.Start >= span.End)
            {
                continue;
            }
            else if (span.Start <= edit.Span.Start && edit.Span.End <= span.End)
            {
                containedDelta += editDelta;
            }
            else
            {
                start = 0;
                end = 0;
                return false;
            }
        }

        start = span.Start + startDelta;
        end = exactEdit is SourceRenameTextEdit replacement
            ? start + Encoding.UTF8.GetByteCount(replacement.NewText)
            : span.End + startDelta + containedDelta;
        return true;
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly record struct OccurrenceKey(string Path, int Start, int End, SourceSemanticSymbolKind Kind,
        int DefinitionStart, int DefinitionEnd, string DefinitionPath);

    private readonly record struct DiagnosticKey(string Path, int Code, DiagnosticSeverity Severity, int Start, int End);

    private readonly record struct SourceLocationKey(string Path, int Start, int End);
}
