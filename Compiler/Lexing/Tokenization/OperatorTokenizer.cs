
using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes symbolic operators using longest-match tokenization, including
/// compound assignments and the reserved increment and decrement operators.
/// </summary>
public sealed class OperatorTokenizer : ILexTokenizer
{
    private static readonly byte[] operatorTokens =
    [
        (byte)'+',
        (byte)'-',
        (byte)'*',
        (byte)'/',
        (byte)'%',
        (byte)'~',
        (byte)'=',
        (byte)'!',
        (byte)'<',
        (byte)'>',
        (byte)'&',
        (byte)'^',
        (byte)'|'
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

        if (current == (byte)'!' && (position + 1 >= bytes.Length || bytes[position + 1] != (byte)'='))
        {
            match = default;
            return false;
        }

        int length = GetOperatorLength(bytes, position);
        match = new LexTokenMatch(LexTokenType.Operator, length);
        return true;
    }

    private static int GetOperatorLength(ReadOnlySpan<byte> bytes, int position)
    {
        if (position + 2 < bytes.Length
            && bytes[position] is (byte)'<' or (byte)'>'
            && bytes[position + 1] == bytes[position]
            && bytes[position + 2] == (byte)'=')
        {
            return 3;
        }

        return IsTwoCharacterOperator(bytes, position) ? 2 : 1;
    }

    private static bool IsTwoCharacterOperator(ReadOnlySpan<byte> bytes, int position)
    {
        if (position + 1 >= bytes.Length)
        {
            return false;
        }

        byte current = bytes[position];
        byte next = bytes[position + 1];

        return current switch
        {
            (byte)'+' or (byte)'-' => next == current || next == (byte)'=',
            (byte)'*' or (byte)'/' or (byte)'%' or (byte)'&' or (byte)'^' or (byte)'|' => next == (byte)'=',
            (byte)'<' or (byte)'>' => next == (byte)'=' || next == current,
            (byte)'=' or (byte)'!' => next == (byte)'=',
            _ => false
        };
    }
}