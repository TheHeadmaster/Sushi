using System.Collections.ObjectModel;
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
        { ";", TokenType.Terminator },
    });
}