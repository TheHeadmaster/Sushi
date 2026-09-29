using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes decimal, binary, octal, and hexadecimal integer literals, including the specified malformed radix-prefixed recovery forms.
/// </summary>
public sealed class IntegerLiteralTokenizer : ILexTokenizer
{
    // Placeholder until diagnostic numbering is settled.
    private const string MissingRadixDigitCode = "SUSE002";
    private const string InvalidRadixDigitCode = "SUSE003";
    private const string InvalidDigitSeparatorCode = "SUSE004";

    /// <inheritdoc />
    public bool CanStart(byte firstByte) => IsAsciiDecimalDigit(firstByte) || firstByte is (byte)'b' or (byte)'o' or (byte)'x';

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

        if (IsAsciiDecimalDigit(bytes[position]))
        {
            match = RecognizeDecimalLiteral(bytes, position, cancellationToken);
            return true;
        }

        if (TryGetRadixPrefix(bytes, position, out IntegerRadix radix))
        {
            match = RecognizeRadixLiteral(snapshot, bytes, position, radix, cancellationToken);
            return true;
        }

        match = default;
        return false;
    }

    /// <summary>
    /// Recognizes a well-formed decimal integer literal using separators only where they occur between decimal digits.
    /// </summary>
    /// <param name="bytes">
    /// The source bytes.
    /// </param>
    /// <param name="position">
    /// The current position of the lexer.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The generated <see cref="LexTokenMatch"/> representing the lexed token.
    /// </returns>
    private static LexTokenMatch RecognizeDecimalLiteral(ReadOnlySpan<byte> bytes, int position, CancellationToken cancellationToken)
    {
        int end = position + 1;

        while (end < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsAsciiDecimalDigit(bytes[end]))
            {
                end++;
                continue;
            }

            if (bytes[end] == (byte)'_' && end + 1 < bytes.Length && IsAsciiDecimalDigit(bytes[end - 1]) && IsAsciiDecimalDigit(bytes[end + 1]))
            {
                end++;
                continue;
            }

            break;
        }

        return new LexTokenMatch(LexTokenType.IntegerLiteral, end - position);
    }

    /// <summary>
    /// Recognizes a radix-prefixed integer literal and preserves malformed numeric-looking body text according to lexical recovery rules.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot.
    /// </param>
    /// <param name="bytes">
    /// The source bytes.
    /// </param>
    /// <param name="position">
    /// The current position of the lexer.
    /// </param>
    /// <param name="radix">
    /// The radix prefix for the literal.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The generated <see cref="LexTokenMatch"/> representing the lexed token.
    /// </returns>
    private static LexTokenMatch RecognizeRadixLiteral(SourceSnapshot snapshot, ReadOnlySpan<byte> bytes, int position, IntegerRadix radix, CancellationToken cancellationToken)
    {
        int bodyStart = position + 2;
        int end = bodyStart;

        while (end < bytes.Length && IsRadixBodyByte(bytes[end], radix))
        {
            cancellationToken.ThrowIfCancellationRequested();
            end++;
        }

        List<SushiDiagnostic> diagnostics = [];
        bool hasDigitCharacter = false;

        for (int index = bodyStart; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte value = bytes[index];

            if (value == (byte)'_')
            {
                if (!IsValidSeparatorPlacement(bytes, bodyStart, end, index, radix))
                {
                    diagnostics.Add(new SushiDiagnostic(
                        InvalidDigitSeparatorCode,
                        "Integer digit separator must occur between two digits valid for the literal's radix.",
                        DiagnosticSeverity.Error,
                        new SourceSpan(snapshot, index, index + 1)));
                }

                continue;
            }

            hasDigitCharacter = true;

            if (!IsDigitForRadix(value, radix))
            {
                diagnostics.Add(new SushiDiagnostic(
                    InvalidRadixDigitCode,
                    $"Digit '{(char)value}' is not valid in a {GetRadixName(radix)} integer literal.",
                    DiagnosticSeverity.Error,
                    new SourceSpan(snapshot, index, index + 1)));
            }
        }

        if (!hasDigitCharacter)
        {
            diagnostics.Add(new SushiDiagnostic(
                MissingRadixDigitCode,
                "Radix-prefixed integer literal requires at least one digit.",
                DiagnosticSeverity.Error,
                new SourceSpan(snapshot, position, position + 2)));
        }

        return new LexTokenMatch(LexTokenType.IntegerLiteral, end - position, diagnostics.Count == 0 ? null : diagnostics);
    }

    /// <summary>
    /// Determines whether a lowercase radix prefix begins at the specified source position.
    /// </summary>
    /// <param name="bytes">
    /// The source bytes to check.
    /// </param>
    /// <param name="position">
    /// The position to check for the radix prefix.
    /// </param>
    /// <param name="radix">
    /// The radix prefix if found. Defaults to <see cref="IntegerRafix.Binary"/> if it wasn't found.
    /// </param>
    /// <returns>
    /// True if the radix prefix was found. False otherwise.
    /// </returns>
    private static bool TryGetRadixPrefix(ReadOnlySpan<byte> bytes, int position, out IntegerRadix radix)
    {
        if (position + 1 >= bytes.Length || bytes[position + 1] != (byte)'#')
        {
            radix = default;
            return false;
        }

        radix = bytes[position] switch
        {
            (byte)'b' => IntegerRadix.Binary,
            (byte)'o' => IntegerRadix.Octal,
            (byte)'x' => IntegerRadix.Hexadecimal,
            _ => default
        };

        return radix != default;
    }

    /// <summary>
    /// Determines whether a byte belongs to the recoverable numeric-looking body of a radix-prefixed literal.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <param name="radix">
    /// The radix to check against.
    /// </param>
    /// <returns>
    /// True if the byte is a legal byte for the specified radix. False otherwise.
    /// </returns>
    private static bool IsRadixBodyByte(byte value, IntegerRadix radix)
        => IsAsciiDecimalDigit(value) || value == (byte)'_' || (radix == IntegerRadix.Hexadecimal && IsAsciiHexadecimalLetter(value));

    /// <summary>
    /// Determines whether an underscore occurs between two digits valid for the selected radix.
    /// </summary>
    /// <param name="bytes">
    /// The source bytes to check within.
    /// </param>
    /// <param name="bodyStart">
    /// The starting index of the slice to check.
    /// </param>
    /// <param name="bodyEnd">
    /// The ending index of the slice to check.
    /// </param>
    /// <param name="position">
    /// The proposed placement of the separator.
    /// </param>
    /// <param name="radix">
    /// The radix to check against.
    /// </param>
    /// <returns>
    /// True if the <see cref="position"/> is a valid position within the slice with respect to the specified radix. False otherwise.
    /// </returns>
    private static bool IsValidSeparatorPlacement(ReadOnlySpan<byte> bytes, int bodyStart, int bodyEnd, int position, IntegerRadix radix)
        => position > bodyStart
            && position + 1 < bodyEnd
            && IsDigitForRadix(bytes[position - 1], radix)
            && IsDigitForRadix(bytes[position + 1], radix);

    /// <summary>
    /// Determines whether a source byte is a valid digit for the selected radix.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <param name="radix">
    /// The radix to check the value against.
    /// </param>
    /// <returns>
    /// True if the source byte is a valid digit respective to the specified radix. False otherwise.
    /// </returns>
    private static bool IsDigitForRadix(byte value, IntegerRadix radix)
        => radix switch
        {
            IntegerRadix.Binary => value is (byte)'0' or (byte)'1',
            IntegerRadix.Octal => value is >= (byte)'0' and <= (byte)'7',
            IntegerRadix.Hexadecimal => IsAsciiDecimalDigit(value) || IsAsciiHexadecimalLetter(value),
            _ => false
        };

    /// <summary>
    /// Determines whether a source byte is an ASCII decimal digit.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <returns>
    /// True if the source byte is an ASCII decimal digit. False otherwise.
    /// </returns>
    private static bool IsAsciiDecimalDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    /// <summary>
    /// Determines whether a source byte is an ASCII hexadecimal letter digit.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <returns>
    /// True if the source byte is an ASCII hex letter digit. False otherwise.
    /// </returns>
    private static bool IsAsciiHexadecimalLetter(byte value) => value is (>= (byte)'A' and <= (byte)'F') or (>= (byte)'a' and <= (byte)'f');

    /// <summary>
    /// Returns the source-language name of the selected radix for diagnostics.
    /// </summary>
    /// <param name="radix">
    /// The radix to get the source-language name for.
    /// </param>
    /// <returns>
    /// The source-language name as a string.
    /// </returns>
    private static string GetRadixName(IntegerRadix radix)
        => radix switch
        {
            IntegerRadix.Binary => "binary",
            IntegerRadix.Octal => "octal",
            IntegerRadix.Hexadecimal => "hexadecimal",
            _ => throw new ArgumentOutOfRangeException(nameof(radix))
        };

    /// <summary>
    /// Identifies one of Sushi's explicitly prefixed integer radices.
    /// </summary>
    private enum IntegerRadix
    {
        Binary = 2,
        Octal = 8,
        Hexadecimal = 16
    }
}