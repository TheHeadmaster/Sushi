using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Maintains a position within a source file's lexical stream and provides token navigation without changing the underlying lexical representation.
/// </summary>
/// <param name="lexerResult">
/// The complete lexical result, including trivia and recovery elements.
/// </param>
/// <param name="cancellationToken">
/// The token used to cancel parsing.
/// </param>
internal sealed class ParserCursor(LexerResult lexerResult, CancellationToken cancellationToken)
{
    private readonly LexerResult lexerResult = lexerResult ?? throw new ArgumentNullException(nameof(lexerResult));

    private readonly CancellationToken cancellationToken = cancellationToken;

    /// <summary>
    /// Gets the authoritative source snapshot associated with this cursor.
    /// </summary>
    public SourceSnapshot Snapshot => this.lexerResult.Snapshot;

    /// <summary>
    /// Gets the current index in the complete lexical stream, including trivia.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets the byte position of the next significant lexical element, or the end of the source if no significant elements remain.
    /// </summary>
    public int Position => this.TryPeek(out LexToken token) ? token.Span.Start : this.Snapshot.SourceLength;

    /// <summary>
    /// Inspects a significant lexical element without advancing the cursor. Trivia is ignored,
    /// but Unknown recovery elements remain significant.
    /// </summary>
    /// <param name="token">
    /// The matching lexical element, if present.
    /// </param>
    /// <param name="lookahead">
    /// The number of significant elements to look ahead.
    /// </param>
    /// <returns>
    /// Whether the requested element exists.
    /// </returns>
    public bool TryPeek(out LexToken token, int lookahead = 0) => this.TryFind(lookahead, skipUnknown: false, out token, out _);

    /// <summary>
    /// Consumes the next significant lexical element, including Unknown elements.
    /// </summary>
    /// <param name="token">
    /// The consumed lexical element, if present.
    /// </param>
    /// <returns>
    /// Whether an element was consumed.
    /// </returns>
    public bool TryAdvance(out LexToken token)
    {
        if (!this.TryFind(0, skipUnknown: false, out token, out int tokenIndex))
        {
            return false;
        }

        this.Index = tokenIndex + 1;
        return true;
    }

    /// <summary>
    /// Consumes an identifier or escaped identifier. Unknown elements may be crossed
    /// only when explicitly permitted by the calling grammar production.
    /// </summary>
    /// <param name="token">
    /// The consumed identifier, if matched.
    /// </param>
    /// <param name="skipUnknown">
    /// Whether to cross Unknown recovery elements.
    /// </param>
    /// <returns>
    /// Whether an identifier was consumed.
    /// </returns>
    public bool TryConsumeIdentifier(out LexToken token, bool skipUnknown = false)
    {
        if (this.TryFind(0, skipUnknown, out LexToken candidate, out int tokenIndex)
            && candidate.Type is LexTokenType.Identifier or
            LexTokenType.EscapedIdentifier)
        {
            this.Index = tokenIndex + 1;

            token = candidate;
            return true;
        }

        token = default;
        return false;
    }

    /// <summary>
    /// Consumes a keyword with the exact specified source spelling.
    /// </summary>
    /// <param name="keyword">
    /// The UTF-8 spelling of the expected keyword.
    /// </param>
    /// <param name="token">
    /// The consumed keyword, if matched.
    /// </param>
    /// <returns>
    /// Whether the keyword was consumed.
    /// </returns>
    public bool TryConsumeKeyword(ReadOnlySpan<byte> keyword, out LexToken token)
    {
        if (this.TryFind(0, skipUnknown: false, out LexToken candidate, out int tokenIndex)
            && candidate.Type is LexTokenType.Keyword
            && this.TokenTextEquals(candidate, keyword))
        {
            this.Index = tokenIndex + 1;
            token = candidate;
            return true;
        }

        token = default;
        return false;
    }

    /// <summary>
    /// Consumes a single-byte punctuation element with the specified spelling.
    /// </summary>
    /// <param name="punctuation">
    /// The expected ASCII punctuation byte.
    /// </param>
    /// <param name="token">
    /// The consumed punctuation, if matched.
    /// </param>
    /// <param name="skipUnknown">
    /// Whether to cross Unknown recovery elements.
    /// </param>
    /// <returns>
    /// Whether the punctuation was consumed.
    /// </returns>
    public bool TryConsumePunctuation(byte punctuation, out LexToken token, bool skipUnknown = false)
    {
        if (this.TryFind(0, skipUnknown, out LexToken candidate, out int tokenIndex)
            && candidate.Type is LexTokenType.Punctuation
            && candidate.Span.Length == 1
            && this.Snapshot.Bytes.Span[candidate.Span.Start] == punctuation)
        {
            this.Index = tokenIndex + 1;
            token = candidate;
            return true;
        }

        token = default;
        return false;
    }

    /// <summary>
    /// Compares the exact source spelling of a lexical element with UTF-8 text.
    /// </summary>
    /// <param name="token">
    /// A lexical element from this cursor's source.
    /// </param>
    /// <param name="expected">
    /// The expected UTF-8 spelling.
    /// </param>
    /// <returns>
    /// Whether the spellings match exactly.
    /// </returns>
    public bool TokenTextEquals(LexToken token, ReadOnlySpan<byte> expected)
        => token.Span.Length == expected.Length
        && this.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual(expected);

    private bool TryFind(int lookahead, bool skipUnknown, out LexToken token, out int tokenIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lookahead);

        for (int i = this.Index; i < this.lexerResult.Tokens.Count; i++)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            LexToken candidate = this.lexerResult.Tokens[i];

            if (IsTrivia(candidate.Type) || (skipUnknown && candidate.Type is LexTokenType.Unknown))
            {
                continue;
            }

            if (lookahead-- > 0)
            {
                continue;
            }

            token = candidate;
            tokenIndex = i;
            return true;
        }

        token = default;
        tokenIndex = -1;
        return false;
    }

    private static bool IsTrivia(LexTokenType type)
        => type is LexTokenType.Whitespace
        or LexTokenType.LineTerminator
        or LexTokenType.LineComment
        or LexTokenType.DocumentationLineComment
        or LexTokenType.BlockComment;
}