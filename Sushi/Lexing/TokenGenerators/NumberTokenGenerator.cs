using System.Text.RegularExpressions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of number literal tokens, such as integers and decimals.
/// </summary>
public sealed partial class NumberTokenGenerator : TokenGenerator
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

        Match match = NumberLiteral().Match(remainingInput);

        if (!match.Success)
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        return Task.FromResult(new TokenGeneratorResult()
        {
            CanGenerate = true,
            Affinity = 0,
            ConsumedCharacters = match.Value.Length,
            Token = new Token()
            {
                Type = TokenType.NumberLiteral,
                Value = match.Value,
                LineNumber = file.GetLineNumber(),
                LinePosition = file.GetLinePosition(),
            }
        });
    }

    /// <summary>
    /// Matches valid number literal strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)?")]
    private static partial Regex NumberLiteral();
}
