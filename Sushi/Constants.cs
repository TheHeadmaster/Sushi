using System.Collections.ObjectModel;
using Sushi.Tokenization;

namespace Sushi;

/// <summary>
/// Holds constants used across the entire application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Contains keywords reserved by the language, and therefore cannot be used as identifiers.
    /// </summary>
    public static ReadOnlyDictionary<string, TokenType> ReservedKeywords { get; } = new ReadOnlyDictionary<string, TokenType>(new Dictionary<string, TokenType>()
    {
        { "bool", TokenType.BoolPrimitive },
        { "true", TokenType.TrueLiteral },
        { "false", TokenType.FalseLiteral },
        { "if", TokenType.If },
        { "then", TokenType.Then },
        { "int32", TokenType.Int32Primitive },
        { "float32", TokenType.Float32Primitive }
    });

    /// <summary>
    /// A symbol table for converting symbols to their respective token types.
    /// </summary>
    public static ReadOnlyDictionary<string, TokenType> Symbols { get; } = new ReadOnlyDictionary<string, TokenType>(new Dictionary<string, TokenType>()
    {
        { ";", TokenType.Terminator },
        { "(", TokenType.OpeningParenthesis },
        { ")", TokenType.ClosingParenthesis },
        { "{", TokenType.OpeningSquiggly },
        { "}", TokenType.ClosingSquiggly },
        { ",", TokenType.Comma },
        { "=", TokenType.AssignmentOperator },
        { "+", TokenType.Plus },
        { "-", TokenType.Minus },
        { "*", TokenType.Asterisk },
        { "/", TokenType.Slash },
    });
}
