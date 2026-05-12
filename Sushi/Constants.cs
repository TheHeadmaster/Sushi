using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi;

/// <summary>
/// Holds constants used across the entire application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// A symbol table for converting symbols to their respective token types.
    /// </summary>
    public static ReadOnlyDictionary<string, TokenType> Symbols { get; } = new ReadOnlyDictionary<string, TokenType>(
        new Dictionary<string, TokenType>()
    {
        { ";", TokenType.Terminator      },
        { ".", TokenType.Dot             },
        { "{", TokenType.OpeningSquiggly },
        { "}", TokenType.ClosingSquiggly }
    });

    /// <summary>
    /// Contains keywords reserved by the language, and therefore cannot be used as identifiers.
    /// </summary>
    public static ReadOnlyDictionary<string, TokenType> ReservedKeywords { get; } = new ReadOnlyDictionary<string, TokenType>(
        new Dictionary<string, TokenType>()
    {
        { "namespace", TokenType.Namespace },
        { "using",     TokenType.Using     },
        { "class",     TokenType.Class     },
        { "public",    TokenType.Public    },
        { "internal",  TokenType.Internal  },
        { "private",   TokenType.Private   },
        { "static",    TokenType.Static    }
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
        //TokenType.Int32Primitive => "int32",
        //TokenType.Float32Primitive => "float32",
        //TokenType.BoolPrimitive => "bool",
        _ => string.Empty
    };

    public static string TryGetModifierKeyword(AccessModifier modifier) => modifier switch
    {
        AccessModifier.Public => "public",
        AccessModifier.Internal => "internal",
        AccessModifier.Private => "private",
        AccessModifier.Static => "static",
        _ => string.Empty
    };
}