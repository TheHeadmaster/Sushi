namespace Sushi.Inspection;

/// <summary>
/// Associates a semantic symbol identity with one occurrence in generated text.
/// Offsets and lengths are measured in UTF-16 code units and the end is exclusive.
/// </summary>
public readonly record struct InspectionSymbolOccurrence
{
    /// <summary>
    /// Gets the symbol represented by this text occurrence.
    /// </summary>
    public InspectionSymbolIdentity Identity { get; }

    /// <summary>
    /// Gets the zero-based generated-text start offset.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the generated-text length in UTF-16 code units.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the exclusive generated-text end offset.
    /// </summary>
    public int End => checked(this.Start + this.Length);

    /// <summary>
    /// Gets whether this occurrence defines the symbol.
    /// </summary>
    public bool IsDefinition { get; }

    /// <summary>
    /// Creates a definition or reference occurrence at a generated-text range.
    /// </summary>
    /// <param name="identity">
    /// The semantic identity associated with the text.
    /// </param>
    /// <param name="start">
    /// The zero-based UTF-16 start offset.
    /// </param>
    /// <param name="length">
    /// The positive UTF-16 length.
    /// </param>
    /// <param name="isDefinition">
    /// True for a definition, or false for a reference.
    /// </param>
    public InspectionSymbolOccurrence(InspectionSymbolIdentity identity, int start, int length, bool isDefinition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        _ = checked(start + length);

        this.Identity = identity;
        this.Start = start;
        this.Length = length;
        this.IsDefinition = isDefinition;
    }
}