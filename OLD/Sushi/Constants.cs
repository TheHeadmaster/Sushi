using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Scope;
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
        { "creator", TokenType.Creator },
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
        { "(", TokenType.OpeningParenthesis },
        { ")", TokenType.ClosingParenthesis },
        { "{", TokenType.OpeningSquiggly },
        { "}", TokenType.ClosingSquiggly },
        { ",", TokenType.Comma },
        { "=", TokenType.Assignment },
        { "+", TokenType.Plus },
        { "-", TokenType.Minus },
        { "*", TokenType.Asterisk },
        { "/", TokenType.Slash }
    });

    /// <summary>
    /// Contains conversions for sushi primitive types to C primitive types.
    /// </summary>
    public static ReadOnlyDictionary<string, string> SushiToCConversions { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()
    {
        { "int32", "int32_t" },
        { "float32", "float" },
        { "bool", "int" }
    });



    public static ReadOnlyCollection<TokenType> PrimitiveTokens { get; } = new
    ([
        TokenType.BoolPrimitive,
        TokenType.Int32Primitive,
        TokenType.Float32Primitive,
    ]);

    /// <summary>
    /// Contains the primitive types that are automatically resolved without a namespace (because they don't belong to one).
    /// </summary>
    /// <returns>
    /// The list of primitive resolved types.
    /// </returns>
    public static ReadOnlyCollection<SushiType> PrimitiveResolvedTypes { get; } = new
    ([
        new() { Name = "int32", FilePath = string.Empty, Namespace = string.Empty },
        new() { Name = "float32", FilePath = string.Empty, Namespace = string.Empty },
        new() { Name = "bool", FilePath = string.Empty, Namespace = string.Empty }
    ]);
}
