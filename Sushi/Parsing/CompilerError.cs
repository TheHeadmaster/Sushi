using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing;

/// <summary>
/// Represents a compiler error that is generated while parsing.
/// </summary>
public sealed class CompilerError(Token token)
{
    /// <summary>
    /// The line number of the compiler error.
    /// </summary>
    public int LineNumber { get; set; } = token.LineNumber;

    /// <summary>
    /// The line position of the compiler error.
    /// </summary>
    public int LinePosition { get; set; } = token.LinePosition;

    /// <summary>
    /// The current line of source code that the compiler error occured in.
    /// </summary>
    public string CurrentLine { get; set; } = token.CurrentLine;

    /// <summary>
    /// The specific reason that the error was thrown.
    /// </summary>
    public required string ErrorReason { get; set; }
}
