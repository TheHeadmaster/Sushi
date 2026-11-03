using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Validates and rebases local declaration tokens retained by cached function representations.
/// </summary>
internal static class InspectionLocalDefinitions
{
    internal static IReadOnlyDictionary<int, SourceSpan> Empty { get; } =
        new ReadOnlyDictionary<int, SourceSpan>(new Dictionary<int, SourceSpan>());

    /// <summary>
    /// Converts bound source spans into immutable declaration-relative cache offsets.
    /// </summary>
    /// <param name="definitions">
    /// The local-index to source-name mapping produced by the body binder.
    /// </param>
    /// <param name="declaration">
    /// The selected function declaration in its authoritative snapshot.
    /// </param>
    /// <param name="document">
    /// The formatted representation whose local identities must have entries in the mapping.
    /// </param>
    /// <param name="functionSymbol">
    /// The selected callable identity that owns the representation.
    /// </param>
    /// <param name="cachedDefinitions">
    /// Receives the validated immutable relative offsets.
    /// </param>
    /// <returns>
    /// True when every local identity in the representation has one valid source token.
    /// </returns>
    internal static bool TryCapture(IReadOnlyDictionary<int, SourceSpan> definitions, SourceSpan declaration,
        InspectionDocument document, string functionSymbol, out IReadOnlyList<CachedLocalDefinition> cachedDefinitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        cachedDefinitions = Array.Empty<CachedLocalDefinition>();
        if (declaration.Snapshot is null || !declaration.Snapshot.IsValidUtf8
            || !HasValidLocalIdentityCoverage(definitions.Keys, declaration, document, functionSymbol))
        {
            return false;
        }

        List<CachedLocalDefinition> captured = [];
        HashSet<(int Start, int End)> spans = [];
        int previousEnd = declaration.Start;
        foreach (KeyValuePair<int, SourceSpan> item in definitions.OrderBy(item => item.Key))
        {
            SourceSpan span = item.Value;
            if (!ReferenceEquals(span.Snapshot, declaration.Snapshot)
                || span.Start < declaration.Start || span.Start >= span.End || span.End > declaration.End
                || span.End > span.Snapshot.SourceLength || !span.Snapshot.IsValidUtf8
                || !IsUtf8Boundary(span.Snapshot.Bytes.Span, span.Start) || !IsUtf8Boundary(span.Snapshot.Bytes.Span, span.End)
                || span.Start < previousEnd || !spans.Add((span.Start, span.End)))
            {
                return false;
            }

            captured.Add(new CachedLocalDefinition(item.Key, span.Start - declaration.Start, span.End - declaration.Start));
            previousEnd = span.End;
        }

        cachedDefinitions = Array.AsReadOnly(captured.ToArray());
        return true;
    }

    /// <summary>
    /// Reconstructs immutable source spans against the requesting inspection's declaration snapshot.
    /// </summary>
    /// <param name="cachedDefinitions">
    /// Declaration-relative offsets captured from a compatible function body.
    /// </param>
    /// <param name="declaration">
    /// The current selected declaration and authoritative source snapshot.
    /// </param>
    /// <param name="document">
    /// The formatted representation whose local identities must have entries in the offsets.
    /// </param>
    /// <param name="functionSymbol">
    /// The selected callable identity that owns the representation.
    /// </param>
    /// <param name="definitions">
    /// Receives the rebased read-only local-index to source-token map.
    /// </param>
    /// <returns>
    /// True when all offsets remain valid within the current declaration and source snapshot.
    /// </returns>
    internal static bool TryRebase(IReadOnlyList<CachedLocalDefinition>? cachedDefinitions, SourceSpan declaration,
        InspectionDocument document, string functionSymbol, out IReadOnlyDictionary<int, SourceSpan> definitions)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        definitions = Empty;
        if (cachedDefinitions is null)
        {
            return false;
        }

        if (declaration.Snapshot is null || declaration.Start < 0 || declaration.Start >= declaration.End
            || declaration.End > declaration.Snapshot.SourceLength || !declaration.Snapshot.IsValidUtf8)
        {
            return false;
        }

        Dictionary<int, SourceSpan> rebased = [];
        HashSet<(int Start, int End)> spans = [];
        int previousEnd = declaration.Start;
        foreach (CachedLocalDefinition item in cachedDefinitions.OrderBy(item => item.Index))
        {
            long start = (long)declaration.Start + item.RelativeStart;
            long end = (long)declaration.Start + item.RelativeEnd;
            if (item.Index < 0 || item.RelativeStart < 0 || item.RelativeStart >= item.RelativeEnd
                || start < previousEnd || end > declaration.End || end > declaration.Snapshot.SourceLength
                || !spans.Add(((int)start, (int)end))
                || !IsUtf8Boundary(declaration.Snapshot.Bytes.Span, (int)start)
                || !IsUtf8Boundary(declaration.Snapshot.Bytes.Span, (int)end)
                || !rebased.TryAdd(item.Index, new SourceSpan(declaration.Snapshot, (int)start, (int)end)))
            {
                return false;
            }

            previousEnd = (int)end;
        }

        if (!HasValidLocalIdentityCoverage(rebased.Keys, declaration, document, functionSymbol))
        {
            return false;
        }

        definitions = new ReadOnlyDictionary<int, SourceSpan>(rebased);
        return true;
    }

    private static bool HasValidLocalIdentityCoverage(IEnumerable<int> localIndexes, SourceSpan declaration, InspectionDocument document,
        string functionSymbol)
    {
        if (!StringComparer.Ordinal.Equals(document.FunctionSymbol, functionSymbol))
        {
            return false;
        }

        int[] indexes = [.. localIndexes];
        HashSet<int> definitions = [.. indexes];
        if (definitions.Count != indexes.Length)
        {
            return false;
        }

        int expectedIndex = 0;
        foreach (int index in definitions.OrderBy(index => index))
        {
            if (index != expectedIndex++)
            {
                return false;
            }
        }

        HashSet<int> representation = [];
        foreach (InspectionSymbolIdentity identity in document.SymbolMetadata.Keys)
        {
            if (identity.Kind is not InspectionSymbolKind.Local)
            {
                continue;
            }

            if (!StringComparer.Ordinal.Equals(identity.FunctionSymbol, document.FunctionSymbol)
                || identity.Index is not int index || !representation.Add(index))
            {
                return false;
            }
        }

        return representation.IsSubsetOf(definitions)
            && declaration.Start >= 0 && declaration.Start < declaration.End
            && declaration.End <= declaration.Snapshot.SourceLength;
    }

    private static bool IsUtf8Boundary(ReadOnlySpan<byte> bytes, int offset)
        => offset >= 0 && offset <= bytes.Length && (offset == bytes.Length || (bytes[offset] & 0b1100_0000) != 0b1000_0000);
}

/// <summary>
/// Stores one local declaration token relative to its containing function declaration.
/// </summary>
/// <param name="Index">
/// The local's existing function-unique compiler identity.
/// </param>
/// <param name="RelativeStart">
/// The UTF-8 byte offset from the function declaration start to the name token start.
/// </param>
/// <param name="RelativeEnd">
/// The exclusive UTF-8 byte offset from the function declaration start to the name token end.
/// </param>
internal sealed record CachedLocalDefinition(int Index, int RelativeStart, int RelativeEnd);
