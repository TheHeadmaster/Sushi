using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Performs lexical analysis of Sushi source text.
/// </summary>
public sealed class SourceLexer : Lexer
{
    private static readonly ILexTokenizer[] tokenizers =
    [
        new CommentTokenizer(),
        new WhitespaceTokenizer()
    ];

    /// <inheritdoc />
    public override LexerResult Lex(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        List<LexToken> tokens = [];
        List<SushiDiagnostic> diagnostics = [];

        int position = 0;

        while (position < snapshot.SourceLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexTokenMatch? bestMatch = null;

            foreach (ILexTokenizer tokenizer in tokenizers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetBestMatch(snapshot, position, cancellationToken, out LexTokenMatch match))
                {
                    CommitMatch(snapshot, position, match, tokens, diagnostics);
                    position += match.Length;
                    continue;
                }

                int unknownEnd = FindUnknownEnd(snapshot, position, cancellationToken);

                tokens.Add(new LexToken(LexTokenType.Unknown, new SourceSpan(snapshot, position, unknownEnd)));
                position = unknownEnd;
            }

            position++;
        }

        return new LexerResult(snapshot, [.. tokens], [.. diagnostics]);
    }

    /// <summary>
    /// Finds the longest lexical match beginning at the specified source position.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot being tokenized.
    /// </param>
    /// <param name="position">
    /// The canonical byte position at which recognition begins.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <param name="match">
    /// The longest recognized lexical match when recognition succeeds.
    /// </param>
    /// <returns>
    /// True if a tokenizer recognizes a lexical element at the specified position. False otherwise.
    /// </returns>
    private static bool TryGetBestMatch(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match)
    {
        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        bool found = false;
        match = default;

        foreach (ILexTokenizer tokenizer in tokenizers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!tokenizer.CanStart(bytes[position]) || !tokenizer.TryRecognize(snapshot, position, cancellationToken, out LexTokenMatch candidate))
            {
                continue;
            }

            if (candidate.Length <= 0 || candidate.Length > snapshot.SourceLength - position)
            {
                throw new InvalidOperationException($"{tokenizer.GetType().Name} produced an invalid lexical match length of {candidate.Length} at byte position {position}.");
            }

            if (!found || candidate.Length > match.Length)
            {
                match = candidate;
                found = true;
                continue;
            }

            if (candidate.Length == match.Length && candidate.Type != match.Type)
            {
                throw new InvalidOperationException($"Ambiguous lexical tokenization at byte position {position}: {match.Type} and {candidate.Type} both consume {candidate.Length} bytes.");
            }
        }

        return found;
    }

    /// <summary>
    /// Commits a recognized lexical match to the token and diagnostic streams.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot containing the matched lexical element.
    /// </param>
    /// <param name="position">
    /// The starting byte position of the match.
    /// </param>
    /// <param name="match">
    /// The lexical match to commit.
    /// </param>
    /// <param name="tokens">
    /// The token stream receiving the recognized lexical element.
    /// </param>
    /// <param name="diagnostics">
    /// The diagnostic collection receiving diagnostics associated with the recognized lexical element.
    /// </param>
    private static void CommitMatch(SourceSnapshot snapshot, int position, LexTokenMatch match, List<LexToken> tokens, List<SushiDiagnostic> diagnostics)
    {
        tokens.Add(new LexToken(match.Type, new SourceSpan(snapshot, position, position + match.Length)));

        if (match.Diagnostics is not null)
        {
            diagnostics.AddRange(match.Diagnostics);
        }
    }

    /// <summary>
    /// Finds the end of a contiguous unrecognized source run.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position at which the unrecognized run begins.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The exclusive byte position at which normal lexical recognition can resume, or the end of the source.
    /// </returns>
    private static int FindUnknownEnd(SourceSnapshot snapshot, int position, CancellationToken cancellationToken)
    {
        int end = position + 1;

        while (end < snapshot.SourceLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetBestMatch(snapshot, end, cancellationToken, out _))
            {
                break;
            }

            end++;
        }

        return end;
    }
}