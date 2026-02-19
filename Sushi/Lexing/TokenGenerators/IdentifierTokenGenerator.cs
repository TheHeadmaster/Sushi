using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of identifier tokens.
/// </summary>
public sealed partial class IdentifierTokenGenerator : TokenGenerator
{
    /// <inheritdoc />
    public override Task<TokenGeneratorResult> TryGenerate(TokenFile file)
    {
        string? remainingInput = file.GetRemainingInput();

        // Don't even bother lexing if the remaining input is empty.
        if (string.IsNullOrWhiteSpace(remainingInput))
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        Match match = Identifier().Match(remainingInput);

        if (!match.Success)
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        if (!match.Value.StartsWith("@", StringComparison.InvariantCultureIgnoreCase) && Constants.ReservedKeywords.Contains(match.Value))
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        return Task.FromResult(new TokenGeneratorResult()
        {
            CanGenerate = true,
            Affinity = 800,
            ConsumedCharacters = match.Length,
            Token = new Token()
            {
                Type = TokenType.Identifier,
                Value = match.Value.Replace("@", string.Empty, StringComparison.InvariantCultureIgnoreCase),
                LineNumber = file.GetLineNumber(),
                LinePosition = file.GetLinePosition()
            }
        });
    }

    /// <summary>
    /// Matches valid identifier strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^@?[a-zA-Z][a-zA-Z0-9]*")]
    private static partial Regex Identifier();
}
