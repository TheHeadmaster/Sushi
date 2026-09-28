using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes identifiers, escaped identifiers, keywords, and Boolean literals.
/// </summary>
public sealed class WordTokenizer : ILexTokenizer
{
    private static readonly byte[][] keywordSpellings =
    [
        "not"u8.ToArray(),
        "and"u8.ToArray(),
        "or"u8.ToArray(),
        "if"u8.ToArray(),
        "else"u8.ToArray(),
        "switch"u8.ToArray(),
        "case"u8.ToArray(),
        "default"u8.ToArray(),
        "noop"u8.ToArray(),
        "while"u8.ToArray(),
        "do"u8.ToArray(),
        "for"u8.ToArray(),
        "foreach"u8.ToArray(),
        "in"u8.ToArray(),
        "break"u8.ToArray(),
        "continue"u8.ToArray(),
        "return"u8.ToArray(),
        "propagate"u8.ToArray(),
        "panic"u8.ToArray(),
        "exit"u8.ToArray(),
        "namespace"u8.ToArray(),
        "package"u8.ToArray(),
        "using"u8.ToArray(),
        "alias"u8.ToArray(),
        "except"u8.ToArray(),
        "global"u8.ToArray(),
        "typeof"u8.ToArray(),
        "is"u8.ToArray(),
        "exactly"u8.ToArray(),
        "inherits"u8.ToArray(),
        "implements"u8.ToArray(),
        "extends"u8.ToArray(),
        "leaf"u8.ToArray(),
        "of"u8.ToArray(),
        "public"u8.ToArray(),
        "internal"u8.ToArray(),
        "protected"u8.ToArray(),
        "private"u8.ToArray(),
        "static"u8.ToArray(),
        "void"u8.ToArray()
    ];

    private static readonly byte[][] booleanLiteralSpellings =
    [
        "true"u8.ToArray(),
        "false"u8.ToArray(),
    ];

    private static readonly byte[][] builtInIntegerTypeSpellings =
    [
        "int8"u8.ToArray(),
        "uint8"u8.ToArray(),
        "int16"u8.ToArray(),
        "uint16"u8.ToArray(),
        "int32"u8.ToArray(),
        "uint32"u8.ToArray(),
        "int64"u8.ToArray(),
        "uint64"u8.ToArray(),
        "int128"u8.ToArray(),
        "uint128"u8.ToArray(),
    ];

    /// <inheritdoc />
    public bool CanStart(byte firstByte) => firstByte == (byte)'@' || IsIdentifierStart(firstByte);

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

        if (bytes[position] == (byte)'@')
        {
            return TryRecognizeEscapedIdentifier(bytes, position, cancellationToken, out match);
        }

        if (!IsIdentifierStart(bytes[position]))
        {
            match = default;
            return false;
        }

        int end = FindIdentifierEnd(bytes, position, cancellationToken);

        ReadOnlySpan<byte> spelling = bytes[position..end];

        if (IsAllUnderscores(spelling))
        {
            match = default;
            return false;
        }

        match = new LexTokenMatch(ClassifyIdentifier(spelling), end - position);

        return true;
    }

    /// <summary>
    /// Recognizes an escaped identifier whose underlying spelling is reserved by the language.
    /// </summary>
    /// <param name="bytes">
    /// The canonical source bytes being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position of the <c>@</c> escape prefix.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <param name="match">
    /// The escaped-identifier match when recognition succeeds.
    /// </param>
    /// <returns>
    /// True when the source contains a valid escaped identifier at the specified position. False otherwise.
    /// </returns>
    private static bool TryRecognizeEscapedIdentifier(ReadOnlySpan<byte> bytes, int position, CancellationToken cancellationToken, out LexTokenMatch match)
    {
        int identifierStart = position + 1;

        if (identifierStart >= bytes.Length || !IsIdentifierStart(bytes[identifierStart]))
        {
            match = default;
            return false;
        }

        int end = FindIdentifierEnd(bytes, identifierStart, cancellationToken);

        ReadOnlySpan<byte> spelling = bytes[identifierStart..end];

        if (!IsReservedIdentifier(spelling))
        {
            match = default;
            return false;
        }

        match = new LexTokenMatch(LexTokenType.EscapedIdentifier, end - position);

        return true;
    }

    /// <summary>
    /// Finds the end of the maximal identifier-shaped source sequence.
    /// </summary>
    /// <param name="bytes">
    /// The canonical source bytes being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position of the first identifier character.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The exclusive byte position following the identifier-shaped sequence.
    /// </returns>
    private static int FindIdentifierEnd(ReadOnlySpan<byte> bytes, int position, CancellationToken cancellationToken)
    {
        int end = position + 1;

        while (end < bytes.Length && IsIdentifierPart(bytes[end]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            end++;
        }

        return end;
    }

    /// <summary>
    /// Classifies a valid ordinary identifier spelling after maximal length has been determined.
    /// </summary>
    /// <param name="spelling">
    /// The complete identifier-shaped source spelling.
    /// </param>
    /// <returns>
    /// The lexical identity assigned to the spelling.
    /// </returns>
    private static LexTokenType ClassifyIdentifier(ReadOnlySpan<byte> spelling)
    {
        if (MatchesAny(spelling, booleanLiteralSpellings))
        {
            return LexTokenType.BooleanLiteral;
        }

        if (MatchesAny(spelling, keywordSpellings))
        {
            return LexTokenType.Keyword;
        }

        return LexTokenType.Identifier;
    }

    /// <summary>
    /// Determines whether an identifier-shaped spelling is reserved and may therefore use the <c>@</c> escape syntax.
    /// </summary>
    /// <param name="spelling">
    /// The identifier-shaped spelling without the escape prefix.
    /// </param>
    /// <returns>
    /// True when the spelling is reserved by the current language version. False otherwise.
    /// </returns>
    private static bool IsReservedIdentifier(ReadOnlySpan<byte> spelling)
        => MatchesAny(spelling, keywordSpellings)
            || MatchesAny(spelling, booleanLiteralSpellings)
            || MatchesAny(spelling, builtInIntegerTypeSpellings);

    /// <summary>
    /// Determines whether a source byte may begin an ordinary identifier.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <returns>
    /// True when the source byte is a valid beginning for an ordinary identifier. False otherwise.
    /// </returns>
    private static bool IsIdentifierStart(byte value) => IsAsciiLetter(value) || value == (byte)'_';

    /// <summary>
    /// Determines whether a source byte may continue an ordinary identifier.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <returns>
    /// True when the source byte is a valid part for an ordinary identifier. False otherwise.
    /// </returns>
    private static bool IsIdentifierPart(byte value) => IsIdentifierStart(value) || value is >= (byte)'0' and <= (byte)'9';

    /// <summary>
    /// Determines whether a source byte is an ASCII letter.
    /// </summary>
    /// <param name="value">
    /// The source byte to check.
    /// </param>
    /// <returns>
    /// True when the source byte is an ASCII letter. False otherwise.
    /// </returns>
    private static bool IsAsciiLetter(byte value) => value is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z');

    /// <summary>
    /// Determines whether an identifier-shaped spelling consists entirely of underscores.
    /// </summary>
    /// <param name="spelling">
    /// The identifier-shaped spelling without the escape prefix.
    /// </param>
    /// <returns>
    /// True if the identifier is comprised entirely of underscore characters. False otherwise.
    /// </returns>
    private static bool IsAllUnderscores(ReadOnlySpan<byte> spelling)
    {
        foreach (byte value in spelling)
        {
            if (value != (byte)'_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a spelling exactly matches one of the supplied language spellings.
    /// </summary>
    /// <param name="spelling">
    /// The identifier-shaped spelling without the escape prefix.
    /// </param>
    /// <param name="candidates">
    /// The candidates to choose from.
    /// </param>
    /// <returns>
    /// True if the spelling exactly matches a candidate. False otherwise.
    /// </returns>
    private static bool MatchesAny(ReadOnlySpan<byte> spelling, byte[][] candidates)
    {
        foreach (byte[] candidate in candidates)
        {
            if (spelling.SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }
}