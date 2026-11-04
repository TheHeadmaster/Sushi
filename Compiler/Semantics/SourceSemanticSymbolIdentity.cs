using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Describes a resolved symbol by its authoritative declaration token and compiler symbol category.
/// </summary>
/// <param name="Kind">
/// The semantic category that prevents cross-kind matches.
/// </param>
/// <param name="DefinitionSnapshot">
/// The snapshot containing the selected declaration.
/// </param>
/// <param name="Start">
/// The declaration token's UTF-8 start offset.
/// </param>
/// <param name="End">
/// The declaration token's UTF-8 end offset.
/// </param>
/// <param name="Index">
/// The bound parameter or local ordinal, when applicable.
/// </param>
internal readonly record struct SourceSemanticSymbolIdentity(
    SourceSemanticSymbolKind Kind,
    SourceSnapshot DefinitionSnapshot,
    int Start,
    int End,
    int Index)
{
    /// <summary>
    /// Creates an identity only for symbols with compiler-established source declarations.
    /// </summary>
    /// <param name="symbol">
    /// The resolved symbol whose declaration provenance is checked.
    /// </param>
    /// <param name="identity">
    /// The identity when the symbol is supported.
    /// </param>
    /// <returns>
    /// True when the symbol has a valid source declaration identity.
    /// </returns>
    internal static bool TryCreate(SourceSemanticSymbol symbol, out SourceSemanticSymbolIdentity identity)
    {
        if (symbol.DefinitionSpan is SourceSpan definition
            && definition.Snapshot is not null
            && symbol.Kind is (SourceSemanticSymbolKind.Function or SourceSemanticSymbolKind.Activity
                or SourceSemanticSymbolKind.ActivityMethod or SourceSemanticSymbolKind.Parameter or SourceSemanticSymbolKind.Local)
            && symbol.Index >= 0
            && definition.Start >= 0 && definition.Start < definition.End
            && definition.End <= definition.Snapshot.SourceLength)
        {
            identity = new SourceSemanticSymbolIdentity(symbol.Kind, definition.Snapshot, definition.Start, definition.End, symbol.Index);
            return true;
        }

        identity = default;
        return false;
    }

    /// <summary>
    /// Determines whether another bound symbol has the same declaration in the same source path and semantic category.
    /// </summary>
    /// <param name="symbol">
    /// The candidate symbol collected from the current compilation context.
    /// </param>
    /// <returns>
    /// True only when declaration provenance, category, and declaration-specific ordinal match.
    /// </returns>
    internal bool Matches(SourceSemanticSymbol symbol)
        => TryCreate(symbol, out SourceSemanticSymbolIdentity candidate)
            && this.Kind == candidate.Kind
            && this.Start == candidate.Start
            && this.End == candidate.End
            && this.Index == candidate.Index
            && this.DefinitionSnapshot.Uri.IsSamePath(candidate.DefinitionSnapshot.Uri);
}
