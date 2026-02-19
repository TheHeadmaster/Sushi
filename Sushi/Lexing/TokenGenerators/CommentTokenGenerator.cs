using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing.TokenGenerators;

/// <summary>
/// Handles the generation of comment tokens.
/// </summary>
public sealed class CommentTokenGenerator : TokenGenerator
{
    /// <inheritdoc />
    public override Task<TokenGeneratorResult> TryGenerate(string source, int index)
    {
    }
}
