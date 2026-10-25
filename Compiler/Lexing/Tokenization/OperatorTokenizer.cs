using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes supported symbolic operators using longest-match tokenization.
/// Also preserves increment and decrement as indivisible lexical elements
/// before their expression grammar is implemented.
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
        (byte)'>'
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

        // A standalone ! is not a Sushi logical-negation operator.
        // Logical negation uses the reserved keyword not.
        if (current == (byte)'!' && (position + 1 >= bytes.Length || bytes[position + 1] != (byte)'='))
        {
            match = default;
            return false;
        }

        int length = IsTwoCharacterOperator(bytes, position) ? 2 : 1;

        match = new LexTokenMatch(LexTokenType.Operator, length);
        return true;
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
            (byte)'+' or (byte)'-' => next == current,
            (byte)'<' or (byte)'>' => next is (byte)'=' || next == current,
            (byte)'=' or (byte)'!' => next == (byte)'=',
            _ => false
        };
    }
}