using System.Text.RegularExpressions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of newline tokens.
/// </summary>
public sealed partial class NewlineTokenGenerator : TokenGenerator
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

        // These statements are done this way because we don't know which operating system the file was written on, which may be different from the one our compiler is running on.
        // So let's catch the longest newline first and then fall through the single char newlines.
        if (remainingInput.StartsWith("\r\n", StringComparison.InvariantCultureIgnoreCase))
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = 2,
                Token = new Token()
                {
                    Type = TokenType.Newline,
                    Value = "\r\n",
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }
        else if (remainingInput.StartsWith("\r", StringComparison.InvariantCultureIgnoreCase))
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = 1,
                Token = new Token()
                {
                    Type = TokenType.Newline,
                    Value = "\r",
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }
        else if (remainingInput.StartsWith("\n", StringComparison.InvariantCultureIgnoreCase))
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = 1,
                Token = new Token()
                {
                    Type = TokenType.Newline,
                    Value = "\n",
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }
}