using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of symbol tokens, such as parentheses, brackets, etc.
/// </summary>
public sealed partial class SymbolTokenGenerator : TokenGenerator
{
    /// <inheritdoc />
    public override Task<TokenGeneratorResult> TryGenerate(TokenFile file)
    {
        string? sample = file.Lookahead(1);

        // Don't even bother lexing if the remaining input is empty.
        if (string.IsNullOrWhiteSpace(sample))
        {
            return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
        }

        TokenType? type = sample switch
        {
            _ => null
        };

        if (type is not null)
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = 2,
                Token = new Token()
                {
                    Type = type.Value,
                    Value = sample.ToString(),
                    LinePosition = file.GetLinePosition(),
                    LineNumber = file.GetLineNumber(),
                }
            });
        }

        char symbol = sample[0];

        type = symbol switch
        {
            '=' => TokenType.AssignmentOperator,
            _ => null
        };

        if (type is not null)
        {
            return Task.FromResult(new TokenGeneratorResult()
            {
                CanGenerate = true,
                Affinity = 10,
                ConsumedCharacters = 1,
                Token = new Token()
                {
                    Type = type.Value,
                    Value = symbol.ToString(),
                    LinePosition = file.GetLinePosition(),
                    LineNumber = file.GetLineNumber(),
                }
            });
        }

        return Task.FromResult(new TokenGeneratorResult() { CanGenerate = false });
    }
}
