using System.Numerics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Interprets source-backed integer literal syntax as an exact mathematical integer.
/// </summary>
internal static class IntegerLiteralBinder
{
    public static bool TryBindExactValue(IntegerLiteralExpressionSyntax expression, out BigInteger value)
    {
        ArgumentNullException.ThrowIfNull(expression);

        SyntaxToken token = expression.LiteralToken;

        if (!token.IsSourceBacked)
        {
            value = default;
            return false;
        }

        ReadOnlySpan<byte> source = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        int radix = 10;
        int position = 0;

        if (source.Length >= 2 && source[1] == (byte)'#')
        {
            radix = source[0] switch
            {
                (byte)'b' => 2,
                (byte)'o' => 8,
                (byte)'x' => 16,
                _ => 0
            };

            position = 2;
        }

        if (radix == 0 || position >= source.Length)
        {
            value = default;
            return false;
        }

        value = BigInteger.Zero;
        bool foundDigit = false;

        for (; position < source.Length; position++)
        {
            byte current = source[position];

            if (current == (byte)'_')
            {
                continue;
            }

            int digit = GetDigitValue(current);

            if (digit < 0 || digit >= radix)
            {
                value = default;
                return false;
            }

            value = (value * radix) + digit;
            foundDigit = true;
        }

        return foundDigit;
    }

    private static int GetDigitValue(byte value)
        => value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
            >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
            >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
            _ => -1
        };
}