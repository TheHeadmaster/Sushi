using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
        { "else", TokenType.Else },
        { "do", TokenType.Do },
        { "while", TokenType.While },
        { "create", TokenType.Create },
        { "destroy", TokenType.Destroy },
        { "destroyer", TokenType.Destroyer },
        { "void", TokenType.Void },
        { "using", TokenType.Using },
        { "namespace", TokenType.Namespace },
        { "class", TokenType.Class },
        { "public", TokenType.Public },
        { "internal", TokenType.Internal },
        { "private", TokenType.Private },
        { "static", TokenType.Static },
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
        { "=", TokenType.Assignment },
        { "+", TokenType.Plus },
        { "-", TokenType.Minus },
        { "*", TokenType.Asterisk },
        { "/", TokenType.Slash },
        { ".", TokenType.Dot }
    });

    /// <summary>
    /// Contains conversions for sushi primitive types to C primitive types.
    /// </summary>
    public static ReadOnlyDictionary<string, string> SushiToCConversions { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()
    {
        { "int32", "int32_t" },
        { "float32", "float" }
    });

    /// <summary>
    /// Tries to get the primitive type for the specified token.
    /// </summary>
    /// <param name="token">
    /// The token to try to get the type for. 
    /// </param>
    /// <returns>
    /// A string containing the type or an empty string if it was not found.
    /// </returns>
    public static string TryGetPrimitiveType([NotNull] Token token) => token.Type switch
    {
        TokenType.Int32Primitive => "int32",
        TokenType.Float32Primitive => "float32",
        _ => string.Empty
    };
}
