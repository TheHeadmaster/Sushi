using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes punctuation whose lexical meaning is established by supported Sushi syntax.
/// </summary>
public sealed class PunctuationTokenizer : ILexTokenizer
{
    private static readonly byte[] punctuationTokens = [
        (byte)'.',
        (byte)';',
        (byte)'(',
        (byte)')',
        (byte)'{',
        (byte)'}'
    ];

    /// <inheritdoc />
    public bool CanStart(byte firstByte) => punctuationTokens.Contains(firstByte);

    /// <inheritdoc />
    public bool TryRecognize(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        if (position < 0 || position >= bytes.Length || !this.CanStart(bytes[position]))
        {
            match = default;
            return false;
        }

        match = new LexTokenMatch(LexTokenType.Punctuation, 1);

        return true;
    }
}