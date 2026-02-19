using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of keyword tokens.
/// </summary>
public sealed class KeywordTokenGenerator : TokenGenerator
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

        foreach (string keyword in Constants.ReservedKeywords)
        {
            if (remainingInput.StartsWith(keyword, StringComparison.InvariantCulture))
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
}
