namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Represents a source file during the lexing step.
/// </summary>
public sealed class TokenFile
{
    /// <summary>
    /// The path of the file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The name of the file with extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The raw source code of the file, or the contents.
    /// </summary>
    public required string RawSourceCode { get; init; }

    /// <summary>
    /// The list of tokens that represent the contents of the file, which is created during the lexing step.
    /// </summary>
    public List<Token> Tokens { get; } = [];

    /// <summary>
    /// The current position of the lexer.
    /// </summary>
    public int CurrentPosition { get; set; }

    /// <summary>
    /// Whether the end of file has been reached.
    /// </summary>
    /// <returns>
    /// True if the end of the file has been reached (anything greater than the length of the file). False otherwise.
    /// </returns>
    public bool EndOfFileReached() => this.CurrentPosition > this.RawSourceCode.Length;

    /// <summary>
    /// Whether the last index of the file has been reached.
    /// </summary>
    /// <returns>
    /// True if the lexer is exactly at the last index of the file. False otherwise.
    /// </returns>
    public bool LastIndexOfFileReached() => this.CurrentPosition == this.RawSourceCode.Length;

    /// <summary>
    /// Gets the next <see cref="char"/> in the file.
    /// </summary>
    /// <returns>
    /// The next <see cref="char"/> or <see langword="null"/>.
    /// </returns>
    public char? GetNextChar() => this.LastIndexOfFileReached() ? null : this.RawSourceCode[this.CurrentPosition];

    /// <summary>
    /// Looks ahead by the specified amount of characters. A lookahead of 1 returns the next character plus the character after it.
    /// </summary>
    /// <param name="amount">
    /// The amount of characters to look ahead. If the amount of characters is less than the lookahead, returns as many characters as are left.
    /// </param>
    /// <returns>
    /// The characters in the specified lookahead, or <see langword="null""/> if the last index of the file was already reached.
    /// </returns>
    public string? Lookahead(int amount) => this.LastIndexOfFileReached() ? null : new string([.. this.RawSourceCode[this.CurrentPosition..].Take(amount + 1)]);

    /// <summary>
    /// Gets the current line that the lexer is on.
    /// </summary>
    /// <returns>
    /// The current line or <see langword="null"/> if the end of the file has been reached.
    /// </returns>
    public string? GetCurrentLine() => this.EndOfFileReached() ? null : this.RawSourceCode.Split(Environment.NewLine)[this.GetLineNumber() - 1];

    /// <summary>
    /// Gets the current line number that the lexer is on.
    /// </summary>
    /// <returns>
    /// The current line number or -1 if the end of the file has been reached.
    /// </returns>
    public int GetLineNumber() => this.EndOfFileReached() ? -1 : this.RawSourceCode[..this.CurrentPosition].Split(Environment.NewLine).Length;

    /// <summary>
    /// Gets the remaining input of the source file.
    /// </summary>
    /// <returns>
    /// The remaining input or <see langword="null"/> if the end of the file has been reached.
    /// </returns>
    public string? GetRemainingInput() => this.EndOfFileReached() ? null : this.RawSourceCode[this.CurrentPosition..];

    /// <summary>
    /// Gets the position that the lexer is at in the current line.
    /// </summary>
    /// <returns>
    /// The current line position or -1 if the end of the file has been reached.
    /// </returns>
    public int GetLinePosition()
    {
        string? currentLine = this.GetCurrentLine();

        if (currentLine is null)
        {
            return -1;
        }

        int consumedLinesLength = this.RawSourceCode.Split(Environment.NewLine).Take(this.GetLineNumber() - 1).Sum(c => c.Length + 2);
        return this.CurrentPosition - consumedLinesLength;
    }
}
