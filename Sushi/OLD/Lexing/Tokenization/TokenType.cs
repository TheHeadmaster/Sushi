namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Represents a token type, such as an identifier, type, string, or unknown.
/// </summary>
public enum TokenType
{
    Unknown,
    AssignmentOperator,
    OpeningParenthesis,
    ClosingParenthesis,
    OpeningSquiggly,
    ClosingSquiggly,
    Identifier,
    LineComment,
    BlockComment,
    Whitespace,
    Newline,
    Terminator,
    Keyword,
    NumberLiteral,
    Comma,
}
