namespace Sushi.Lexing.Tokenization;

public enum LexTokenType
{
    Unknown,
    Whitespace,
    LineTerminator,
    LineComment,
    DocumentationLineComment,
    BlockComment,
    Punctuation,
    Identifier,
    EscapedIdentifier,
    Keyword,
    IntegerLiteral,
    BooleanLiteral
}