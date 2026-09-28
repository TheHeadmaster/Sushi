namespace Sushi.Lexing.Tokenization;

public enum LexTokenType
{
    Unknown,
    Whitespace,
    LineTerminator,
    LineComment,
    DocumentationLineComment,
    BlockComment,
    Identifier,
    EscapedIdentifier,
    Keyword,
    IntegerLiteral,
    BooleanLiteral
}