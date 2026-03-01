using System.Text.RegularExpressions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of whitespace tokens.
/// </summary>
public sealed partial class WhitespaceTokenGenerator : TokenGenerator
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

        // Match leading whitespace pattern
        Match match = LeadingWhitespace().Match(remainingInput);

        if (match.Success)
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = match.Length,
                Token = new Token()
                {
                    Type = TokenType.Whitespace,
                    Value = match.Value,
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }

    /// <summary>
    /// Matches on whitespace characters at the beginning of a string.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[\s-[\r\n]]+")]
    private static partial Regex LeadingWhitespace();
}