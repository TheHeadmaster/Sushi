using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes whitespace and line-terminator trivia.
/// </summary>
public sealed class WhitespaceTokenizer : ILexTokenizer
{
    /// <inheritdoc />
    public bool CanStart(byte firstByte)
    {
        return firstByte is
            (byte)' '
            or (byte)'\t'
            or (byte)'\r'
            or (byte)'\n';
    }

    /// <inheritdoc />
    public bool TryRecognize(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        if (position < 0 || position >= bytes.Length)
        {
            match = default;
            return false;
        }

        byte current = bytes[position];

        if (current is (byte)'\r' or (byte)'\n')
        {
            int length =
                current == (byte)'\r'
                && position + 1 < bytes.Length
                && bytes[position + 1] == (byte)'\n'
                    ? 2
                    : 1;

            match = new LexTokenMatch(LexTokenType.LineTerminator, length);

            return true;
        }

        if (current is not ((byte)' ' or (byte)'\t'))
        {
            match = default;
            return false;
        }

        int end = position + 1;

        while (end < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bytes[end] is not ((byte)' ' or (byte)'\t'))
            {
                break;
            }

            end++;
        }

        match = new LexTokenMatch(LexTokenType.Whitespace, end - position);

        return true;
    }
}