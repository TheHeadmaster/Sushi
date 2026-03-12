namespace Sushi.Tokenization;

/// <summary>
/// Represents a token type, such as an identifier, type, string, or unknown.
/// </summary>
public enum TokenType
{
    Unknown,
    Assignment,
    OpeningParenthesis,
    ClosingParenthesis,
    OpeningSquiggly,
    ClosingSquiggly,
    Identifier,
    Terminator,
    NumberLiteral,
    Comma,
    BoolPrimitive,
    TrueLiteral,
    FalseLiteral,
    If,
    Then,
    Else,
    Int32Primitive,
    Float32Primitive,
    Plus,
    Minus,
    Asterisk,
    Slash,
    Using,
    Dot,
    Namespace,
}

