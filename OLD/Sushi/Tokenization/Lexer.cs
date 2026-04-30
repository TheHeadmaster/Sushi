using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;

namespace Sushi.Tokenization;

/// <summary>
/// Handles the scanning and lexing of source files into tokens.
/// </summary>
public sealed partial class Lexer
{





    private static async Task ConsumeTokenWithHighestAffinity([NotNull] TokenFile file)
    {

        else if (IsIdentifier(remainingInput, out string? identifier))
        {
            handled = true;
            tokenValue = identifier;
            type = TokenType.Identifier;
        }
        else if (IsString(remainingInput, out string? stringLiteral))
        {
            handled = true;
            tokenValue = stringLiteral;
            type = TokenType.StringLiteral;
        }
        else if (IsNumber(remainingInput, out string? number))
        {
            handled = true;
            tokenValue = number;
            type = TokenType.NumberLiteral;
        }
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as an identifier.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="identifier">The identifier that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsIdentifier(string remainingInput, [NotNullWhen(true)] out string? identifier)
    {
        identifier = null;

        Match match = Identifier().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        if (!match.Value.StartsWith("@", StringComparison.InvariantCultureIgnoreCase) && Constants.ReservedKeywords.ContainsKey(match.Value))
        {
            return false;
        }

        identifier = match.Value.Replace("@", string.Empty, StringComparison.InvariantCultureIgnoreCase);
        return true;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a number.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="number">The number that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsNumber(string remainingInput, [NotNullWhen(true)] out string? number)
    {
        number = null;

        Match match = NumberLiteral().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        number = match.Value;
        return true;
    }
    

    /// <summary>
    /// Returns whether the specified input can be consumed as a string.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="stringLiteral">The string literal that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsString(string remainingInput, [NotNullWhen(true)] out string? stringLiteral)
    {
        stringLiteral = null;

        Match match = StringLiteral().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        stringLiteral = match.Value;
        return true;
    }

   
    /// <summary>
    /// Matches valid identifier strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^@?[a-zA-Z][a-zA-Z0-9]*")]
    private static partial Regex Identifier();

    /// <summary>
    /// Matches valid string literal strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex("^\"(.*?(?<!\\\\))\"")]
    private static partial Regex StringLiteral();

    /// <summary>
    /// Matches valid number literal strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)?")]
    private static partial Regex NumberLiteral();
}
