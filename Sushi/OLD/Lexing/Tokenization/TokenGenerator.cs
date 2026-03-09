namespace Sushi.Lexing.Tokenization;

/// <summary>
/// A token generator handles the generation of a specific type of token or tokens.
/// Tokens will be handled based on highest affinity.
/// </summary>
public abstract class TokenGenerator
{
    /// <summary>
    /// Tries to generate a token for the specific <see cref="TokenFile"/>.
    /// </summary>
    /// <param name="file">
    /// The file to generate the token from.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the <see cref="TokenGeneratorResult"/>.
    /// </returns>
    public abstract Task<TokenGeneratorResult> TryGenerate(TokenFile file);
}
