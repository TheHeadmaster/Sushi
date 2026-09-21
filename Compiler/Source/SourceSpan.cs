namespace Sushi.Source;

/// <summary>
/// Represents a span of characters from a source file.
/// </summary>
/// <param name="Snapshot">
/// The source snapshot that this span originated from.
/// </param>
/// <param name="Start">
/// The start index of the span.
/// </param>
/// <param name="End">
/// The end index of the span.
/// </param>
public readonly record struct SourceSpan(SourceSnapshot Snapshot, int Start, int End)
{
    /// <summary>
    /// The length of the source span.
    /// </summary>
    public int Length => this.End - this.Start;
}