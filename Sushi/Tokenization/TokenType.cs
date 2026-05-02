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
    Namespace,

    /// <summary>
    /// This is a token that represents an identifier.
    /// </summary>
    Identifier,

    /// <summary>
    /// This is a token that represents a navigation operator.
    /// </summary>
    Dot,

    /// <summary>
    /// This is a token that represents the using keyword.
    /// </summary>
    Using,

    /// <summary>
    /// This is a token that represents the class keyword.
    /// </summary>
    Class,

    /// <summary>
    /// This is a token that represents the public keyword.
    /// </summary>
    Public,

    /// <summary>
    /// This is a token that represents the internal keyword.
    /// </summary>
    Internal,

    /// <summary>
    /// This is a token that represents the private keyword.
    /// </summary>
    Private,

    /// <summary>
    /// This is a token that represents the static keyword.
    /// </summary>
    Static,

    /// <summary>
    /// This is a token that represents an opening squiggly bracket.
    /// </summary>
    OpeningSquiggly,

    /// <summary>
    /// This is a token that represents a closing squiggly bracket.
    /// </summary>
    ClosingSquiggly,
}