using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes the arithmetic operators supported by the initial expression parser,
/// while retaining increment and decrement as indivisible lexical elements.
/// </summary>
public sealed class OperatorTokenizer : ILexTokenizer
{
    private static readonly byte[] operatorTokens = [
        (byte)'+',
        (byte)'-',
        (byte)'*',
        (byte)'/',
        (byte)'%'
    ];

    /// <inheritdoc />
    public bool CanStart(byte firstByte) => operatorTokens.Contains(firstByte);

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

        byte current = bytes[position];

        int length = IsIncrementOrDecrement(position, bytes, current) ? 2 : 1;

        match = new LexTokenMatch(LexTokenType.Operator, length);

        return true;
    }

    private static bool IsIncrementOrDecrement(int position, ReadOnlySpan<byte> bytes, byte current)
        => position + 1 < bytes.Length
            && current is (byte)'+' or (byte)'-'
            && bytes[position + 1] == current;

}