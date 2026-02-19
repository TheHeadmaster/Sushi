using System.Diagnostics.CodeAnalysis;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Represents a result from a <see cref="TokenGenerator.TryGenerate(TokenFile)"/> call.
/// </summary>
public sealed class TokenGeneratorResult
{
    /// <summary>
    /// Whether the <see cref="TokenGenerator"/> can generate a <see cref="Tokenization.Token"/>.
    /// </summary>
    public required bool CanGenerate { get; set; }

    /// <summary>
    /// Affinity determines what token is chosen when competing generators can handle the next input.
    /// Higher affinity gets chosen first. If two tokens are the same affinity, then the one with the
    /// most consumed characters gets priority.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CanGenerate))]
    public int? Affinity { get; set; }

    /// <summary>
    /// The number of characters that would be consumed by the <see cref="Tokenization.Token"/> if it is chosen.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CanGenerate))]
    public int? ConsumedCharacters { get; set; }

    /// <summary>
    /// The generated <see cref="Tokenization.Token"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(CanGenerate))]
    public Token? Token { get; set; }
}
