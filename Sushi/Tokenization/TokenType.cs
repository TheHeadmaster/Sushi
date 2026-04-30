namespace Sushi.Tokenization;

/// <summary>
/// Represents a token type, such as an identifier, type, string, or unknown.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// This is a token that represents a syntax error.
    /// </summary>
    Unknown,

    /// <summary>
    /// This is a token that represents a span of whitespace.
    /// </summary>
    Whitespace,

    /// <summary>
    /// This is a token that represents a newline.
    /// </summary>
    Newline,

    /// <summary>
    /// This is a token that represents a terminator character, i.e. ";".
    /// </summary>
    Terminator,

    /// <summary>
    /// This is a token that represents the namespace keyword.
    /// </summary>
    Namespace
}
