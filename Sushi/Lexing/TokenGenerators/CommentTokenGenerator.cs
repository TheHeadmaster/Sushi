using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of comment tokens.
/// </summary>
public sealed class CommentTokenGenerator : TokenGenerator
{
    /// <inheritdoc />
    public override Task<TokenGeneratorResult> TryGenerate(TokenFile file)
    {
        string? remainingInput = file.GetRemainingInput();

        // Don't even bother parsing if the remaining input is empty.
        if (string.IsNullOrWhiteSpace(remainingInput))
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        // Lexes line comments (i.e. // This is a comment).
        if (remainingInput.StartsWith("//", StringComparison.InvariantCultureIgnoreCase))
        {
            string comment = remainingInput.Split(Environment.NewLine)[0];

            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 1000,
                ConsumedCharacters = comment.Length,
                Token = new Token()
                {
                    Type = TokenType.LineComment,
                    Value = comment,
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }

        // Lexes block comments (i.e. /* This is a comment */).
        if (remainingInput.StartsWith("/*", StringComparison.InvariantCultureIgnoreCase))
        {
            // Lazily gets the next closing */ and gets the content of that.
            string comment = $"{remainingInput.Split("*/")[0]}*/";

            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 1000,
                ConsumedCharacters = comment.Length,
                Token = new Token()
                {
                    Type = TokenType.BlockComment,
                    Value = comment,
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }
}
