namespace Sushi.Diagnostics;

/// <summary>
/// Represents a position in which the associated <see cref="CompilerError"/> was raised at.
/// </summary>
public sealed class ErrorPosition
{
    /// <summary>
    /// The line number that the error was reported at.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// The line position that the error was reported at.
    /// </summary>
    public required int LinePosition { get; init; }

    /// <summary>
    /// The line where the error was reported at.
    /// </summary>
    public required string Line { get; init; }

    /// <summary>
    /// The length of the error. Lexing errors normally have a length of 1, as it's normally an errant character.
    /// Compiler errors often have a span as the error involves a specific token or tokens.
    /// </summary>
    public required int ErrorLength { get; init; }
}
