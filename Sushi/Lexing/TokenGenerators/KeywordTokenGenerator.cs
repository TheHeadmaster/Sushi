using System.Text.RegularExpressions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of keyword tokens.
/// </summary>
public sealed partial class KeywordTokenGenerator : TokenGenerator
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

        Match match = Keyword().Match(remainingInput);

        if (!match.Success)
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        foreach (string keyword in Constants.ReservedKeywords)
        {
            if (match.Value.Equals(keyword, StringComparison.Ordinal))
            {
                return Task.FromResult(new TokenGeneratorResult()
                {
                    CanGenerate = true,
                    Affinity = 1200,
                    ConsumedCharacters = keyword.Length,
                    Token = new Token()
                    {
                        Type = TokenType.Keyword,
                        Value = keyword,
                        LineNumber = file.GetLineNumber(),
                        LinePosition = file.GetLinePosition()
                    }
                });
            }
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }

    /// <summary>
    /// Matches valid keyword strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[a-z][a-z0-9]*")]
    private static partial Regex Keyword();
}
