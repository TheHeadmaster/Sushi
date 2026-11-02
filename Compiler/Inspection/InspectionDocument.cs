namespace Sushi.Inspection;

/// <summary>
/// Contains an immutable generated representation and the semantic occurrences
/// recorded while it was formatted. Compilation snapshot identity is supplied
/// by the future inspection service rather than inferred from this text.
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
    internal InspectionDocument(InspectionStage stage, string functionSymbol, string text, IReadOnlyList<InspectionSymbolOccurrence> occurrences)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(occurrences);

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
    }
}