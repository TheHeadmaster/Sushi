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
        new WhitespaceTokenizer(),
        new PunctuationTokenizer(),
        new WordTokenizer(),
        new IntegerLiteralTokenizer()
    ];

    /// <inheritdoc />
    public override LexerResult Lex(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        List<LexToken> tokens = [];
        HashSet<int> specificallyDiagnosedBoundaries = [];

        int position = 0;
        int encodingIssueIndex = 0;

        while (position < snapshot.SourceLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryCommitEncodingIssue(snapshot, position, ref encodingIssueIndex, tokens, out int encodingIssueEnd))
            {
                position = encodingIssueEnd;
                continue;
            }

            if (TryGetBestMatch(snapshot, position, cancellationToken, out LexTokenMatch match))
            {
                int matchEnd = position + match.Length;

                position = CommitMatch(snapshot, position, match, tokens, diagnosticReporter, ref encodingIssueIndex);

                if (match.DiagnosesFollowingBoundary)
                {
                    specificallyDiagnosedBoundaries.Add(matchEnd);
                }

                continue;
            }

            int unknownEnd = FindUnknownEnd(snapshot, position, encodingIssueIndex, cancellationToken);

            tokens.Add(new LexToken(LexTokenType.Unknown, new SourceSpan(snapshot, position, unknownEnd)));
            position = unknownEnd;
        }

        AddLexicalAdjacencyDiagnostics(snapshot, tokens, diagnosticReporter, specificallyDiagnosedBoundaries);

        return new LexerResult(snapshot, [.. tokens]);
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
    /// Commits a recognized lexical match to the token stream.
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
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter that contains the accumulated diagnostics.
    /// </param>
    /// <param name="encodingIssueIndex">
    /// The index of the next uncommitted encoding issue.
    /// </param>
    /// <returns>
    /// The canonical byte position at which lexical analysis should continue.
    /// </returns>
    private static int CommitMatch(SourceSnapshot snapshot, int position, LexTokenMatch match, List<LexToken> tokens, IDiagnosticReporter diagnosticReporter, ref int encodingIssueIndex)
    {
        int matchEnd = position + match.Length;
        int segmentStart = position;

        while (encodingIssueIndex < snapshot.EncodingIssues.Count)
        {
            SourceEncodingIssue issue = snapshot.EncodingIssues[encodingIssueIndex];

            if (issue.Start >= matchEnd)
            {
                break;
            }

            if (issue.End <= segmentStart)
            {
                encodingIssueIndex++;
                continue;
            }

            if (issue.Start > segmentStart)
            {
                tokens.Add(new LexToken(match.Type, new SourceSpan(snapshot, segmentStart, issue.Start)));
            }

            tokens.Add(new LexToken(LexTokenType.Unknown, new SourceSpan(snapshot, Math.Max(segmentStart, issue.Start), issue.End)));
            
            segmentStart = issue.End;
            encodingIssueIndex++;
        }

        if (segmentStart < matchEnd)
        {
            tokens.Add(new LexToken(match.Type, new SourceSpan(snapshot, segmentStart, matchEnd)));
        }

        if (match.DiagnosticReporter is not null && match.DiagnosticReporter.HasErrors())
        {
            diagnosticReporter.CommitReporter(match.DiagnosticReporter);
        }

        return Math.Max(matchEnd, segmentStart);
    }

    /// <summary>
    /// Finds the end of a contiguous unrecognized source run without
    /// consuming a malformed UTF-8 range.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position at which the unrecognized run begins.
    /// </param>
    /// <param name="encodingIssueIndex">
    /// The index of the next uncommitted encoding issue.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The exclusive byte position at which normal lexical recognition
    /// or encoding recovery begins, or the end of the source.
    /// </returns>
    private static int FindUnknownEnd(SourceSnapshot snapshot, int position, int encodingIssueIndex, CancellationToken cancellationToken)
    {
        int encodingBoundary = encodingIssueIndex < snapshot.EncodingIssues.Count
            ? snapshot.EncodingIssues[encodingIssueIndex].Start
            : snapshot.SourceLength;

        int end = position + 1;

        while (end < snapshot.SourceLength && end < encodingBoundary)
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

    /// <summary>
    /// Commits a malformed UTF-8 range as an unknown lexical element when the current position lies within that range.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot being tokenized.
    /// </param>
    /// <param name="position">
    /// The current canonical byte position.
    /// </param>
    /// <param name="encodingIssueIndex">
    /// The index of the next uncommitted encoding issue.
    /// </param>
    /// <param name="tokens">
    /// The token stream receiving the recovery element.
    /// </param>
    /// <param name="end">
    /// The byte position immediately following the committed encoding issue when an issue is found.
    /// </param>
    /// <returns>
    /// True when a malformed UTF-8 range was committed. False otherwise.
    /// </returns>
    private static bool TryCommitEncodingIssue(SourceSnapshot snapshot, int position, ref int encodingIssueIndex, List<LexToken> tokens, out int end)
    {
        while (encodingIssueIndex < snapshot.EncodingIssues.Count && snapshot.EncodingIssues[encodingIssueIndex].End <= position)
        {
            encodingIssueIndex++;
        }

        if (encodingIssueIndex >= snapshot.EncodingIssues.Count || snapshot.EncodingIssues[encodingIssueIndex].Start > position)
        {
            end = position;
            return false;
        }

        SourceEncodingIssue issue = snapshot.EncodingIssues[encodingIssueIndex++];

        tokens.Add(new LexToken(LexTokenType.Unknown, new SourceSpan(snapshot, position, issue.End)));

        end = issue.End;
        return true;
    }

    /// <summary>
    /// Adds diagnostics for directly adjacent lexical elements whose classifications require source separation.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot containing the lexical elements.
    /// </param>
    /// <param name="tokens">
    /// The completed top-level lexical and recovery stream in source order.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="specificallyDiagnosedBoundaries">
    /// Source positions whose lexical failure is already semantically explained by a more specific diagnostic.
    /// </param>
    private static void AddLexicalAdjacencyDiagnostics(SourceSnapshot snapshot, IReadOnlyList<LexToken> tokens, IDiagnosticReporter diagnosticReporter, HashSet<int> specificallyDiagnosedBoundaries)
    {
        for (int index = 1; index < tokens.Count; index++)
        {
            LexToken previous = tokens[index - 1];
            LexToken current = tokens[index];

            if (previous.Span.End != current.Span.Start || !IsSeparationRequired(previous.Type) || !IsSeparationRequired(current.Type))
            {
                continue;
            }

            int boundary = previous.Span.End;

            if (specificallyDiagnosedBoundaries.Contains(boundary))
            {
                continue;
            }

            diagnosticReporter.GenerateError(ErrorType.MissingLexicalSeparation, new SourceSpan(snapshot, boundary, boundary));
        }
    }

    /// <summary>
    /// Determines whether a lexical classification requires separation from another separation-required lexical element.
    /// </summary>
    /// <param name="type">
    /// The lexical classification to inspect.
    /// </param>
    /// <returns>
    /// True when an element of the specified type requires separation from another separation-required element. False otherwise.
    /// </returns>
    private static bool IsSeparationRequired(LexTokenType type)
        => type is LexTokenType.Identifier
            or LexTokenType.EscapedIdentifier
            or LexTokenType.Keyword
            or LexTokenType.IntegerLiteral
            or LexTokenType.BooleanLiteral;
}