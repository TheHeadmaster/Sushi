using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of terminator tokens.
/// </summary>
public sealed class TerminatorTokenGenerator : TokenGenerator
{
    /// <inheritdoc />
    public override Task<TokenGeneratorResult> TryGenerate(TokenFile file)
    {
        char? nextChar = file.GetNextChar();

        if (nextChar is ';')
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = int.MaxValue,
                ConsumedCharacters = 1,
                Token = new Token()
                {
                    Type = TokenType.Terminator,
                    Value = ";",
                    LineNumber = file.GetLineNumber(),
                    LinePosition = file.GetLinePosition()
                }
            });
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }
}
