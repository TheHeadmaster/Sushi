using System.Collections.ObjectModel;
using Sushi.Intermediate;

namespace Sushi.Inspection;

/// <summary>
/// Contains an immutable generated representation and the semantic occurrences
/// and typed symbol metadata recorded while it was formatted. Compilation snapshot
/// identity remains in the owning inspection result rather than being inferred from this text.
/// </summary>
public sealed class InspectionDocument
{
    /// <summary>
    /// Gets the represented compilation stage.
    /// </summary>
    public InspectionStage Stage { get; }

    /// <summary>
    /// Gets the compiler-generated symbol of the inspected function.
    /// </summary>
    public string FunctionSymbol { get; }

    /// <summary>
    /// Gets the complete generated text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets source-ordered definitions and references in the generated text.
    /// </summary>
    public IReadOnlyList<InspectionSymbolOccurrence> Occurrences { get; }

    /// <summary>
    /// Gets typed formatter metadata for local slots, SSA temporaries, and basic blocks.
    /// </summary>
    public IReadOnlyDictionary<InspectionSymbolIdentity, InspectionSymbolMetadata> SymbolMetadata { get; }

    /// <summary>
    /// Creates an inspection document from text and its accompanying metadata.
    /// </summary>
    /// <param name="stage">
    /// The representation stage.
    /// </param>
    /// <param name="functionSymbol">
    /// The compiler-generated owner function symbol.
    /// </param>
    /// <param name="text">
    /// The complete formatted representation.
    /// </param>
    /// <param name="occurrences">
    /// The occurrences recorded during formatting.
    /// </param>
    internal InspectionDocument(InspectionStage stage, string functionSymbol, string text,
        IReadOnlyList<InspectionSymbolOccurrence> occurrences,
        IReadOnlyDictionary<InspectionSymbolIdentity, InspectionSymbolMetadata> symbolMetadata)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(symbolMetadata);

        InspectionSymbolOccurrence[] ordered = [.. occurrences];

        int previousEnd = 0;

        foreach (InspectionSymbolOccurrence occurrence in ordered)
        {
            if (occurrence.Start < previousEnd || occurrence.End > text.Length)
            {
                throw new ArgumentException("Symbol occurrences must be ordered, nonoverlapping, and within generated text.", nameof(occurrences));
            }

            previousEnd = occurrence.End;
        }

        this.Stage = stage;
        this.FunctionSymbol = functionSymbol;
        this.Text = text;
        this.Occurrences = Array.AsReadOnly(ordered);
        Dictionary<InspectionSymbolIdentity, InspectionSymbolMetadata> metadata = new();
        foreach (KeyValuePair<InspectionSymbolIdentity, InspectionSymbolMetadata> item in symbolMetadata)
        {
            InspectionSymbolMetadata value = item.Value ?? throw new ArgumentException("Symbol metadata values cannot be null.", nameof(symbolMetadata));
            bool hasDefinition = ordered.Any(occurrence => occurrence.Identity == item.Key && occurrence.IsDefinition);
            if (!StringComparer.Ordinal.Equals(item.Key.FunctionSymbol, functionSymbol) || !hasDefinition)
            {
                throw new ArgumentException("Symbol metadata must correspond to a definition owned by this function.", nameof(symbolMetadata));
            }

            bool valid = (item.Key.Kind is (InspectionSymbolKind.Local or InspectionSymbolKind.Temporary)
                    && value is InspectionValueMetadata typedValue && typedValue.Type is not IRType.Void && !string.IsNullOrEmpty(typedValue.DisplayName))
                || (item.Key.Kind is InspectionSymbolKind.BasicBlock && stage is InspectionStage.LoweredIR
                    && value is InspectionBasicBlockMetadata block && !string.IsNullOrEmpty(block.Label));
            if (!valid)
            {
                throw new ArgumentException("Symbol metadata does not match its identity or representation stage.", nameof(symbolMetadata));
            }

            metadata.Add(item.Key, value);
        }

        this.SymbolMetadata = new ReadOnlyDictionary<InspectionSymbolIdentity, InspectionSymbolMetadata>(metadata);
    }
}
