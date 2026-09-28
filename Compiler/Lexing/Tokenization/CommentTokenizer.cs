using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Recognizes line, documentation-line, and nested block comments.
/// </summary>
public sealed class CommentTokenizer : ILexTokenizer
{
    private const string UnterminatedBlockCommentCode = "SUSE001";

    /// <inheritdoc />
    public bool CanStart(byte firstByte) => firstByte == (byte)'/';

    /// <inheritdoc />
    public bool TryRecognize(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        if (position < 0 || position >= bytes.Length || bytes[position] != (byte)'/' || position + 1 >= bytes.Length)
        {
            match = default;
            return false;
        }

        if (bytes[position + 1] == (byte)'/')
        {
            match = RecognizeLineComment(bytes, position, cancellationToken);
            return true;
        }

        if (bytes[position + 1] == (byte)'*')
        {
            match = RecognizeBlockComment(snapshot, bytes, position, cancellationToken);
            return true;
        }

        match = default;
        return false;
    }

    /// <summary>
    /// Recognizes a line comment through the byte immediately preceding its line terminator or the end of the source.
    /// </summary>
    /// <param name="bytes">
    /// The canonical source bytes being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position of the opening <c>//</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The recognized line-comment match.
    /// </returns>
    private static LexTokenMatch RecognizeLineComment(ReadOnlySpan<byte> bytes, int position, CancellationToken cancellationToken)
    {
        LexTokenType type = position + 2 < bytes.Length && bytes[position + 2] == (byte)'/'
            ? LexTokenType.DocumentationLineComment
            : LexTokenType.LineComment;

        int end = position + 2;

        while (end < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bytes[end] is (byte)'\r' or (byte)'\n')
            {
                break;
            }

            end++;
        }

        return new LexTokenMatch(type, end - position);
    }

    /// <summary>
    /// Recognizes a possibly nested block comment and reports an unterminated comment when the source ends before the nesting depth returns to zero.
    /// </summary>
    /// <param name="snapshot">
    /// The source snapshot containing the comment.
    /// </param>
    /// <param name="bytes">
    /// The canonical source bytes being tokenized.
    /// </param>
    /// <param name="position">
    /// The byte position of the opening <c>/*</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lexical analysis.
    /// </param>
    /// <returns>
    /// The recognized block-comment match, including any diagnostic produced by an unterminated comment.
    /// </returns>
    private static LexTokenMatch RecognizeBlockComment(SourceSnapshot snapshot, ReadOnlySpan<byte> bytes, int position, CancellationToken cancellationToken)
    {
        int end = position + 2;
        int depth = 1;

        while (end < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsBlockCommentStart(bytes, end))
            {
                depth++;
                end += 2;
                continue;
            }

            if (IsBlockCommentEnd(bytes, end))
            {
                depth--;
                end += 2;

                if (depth == 0)
                {
                    return new LexTokenMatch(LexTokenType.BlockComment, end - position);
                }

                continue;
            }

            end++;
        }

        SushiDiagnostic diagnostic = new(
            UnterminatedBlockCommentCode,
            "Unterminated block comment.",
            DiagnosticSeverity.Error,
            new SourceSpan(snapshot, position, position + 2));

        return new LexTokenMatch(LexTokenType.BlockComment, end - position, [diagnostic]);
    }

    /// <summary>
    /// Determines whether a nested block comment begins at the specified byte position.
    /// </summary>
    /// <param name="bytes">
    /// The canonical source bytes being inspected.
    /// </param>
    /// <param name="position">
    /// The byte position to inspect.
    /// </param>
    /// <returns>
    /// True when <c>/*</c> begins at the specified position.
    /// </returns>
    private static bool IsBlockCommentStart(ReadOnlySpan<byte> bytes, int position)
        => position + 1 < bytes.Length && bytes[position] == (byte)'/' && bytes[position + 1] == (byte)'*';

    /// <summary>
    /// Determines whether a block comment ends at the specified byte position.
    /// </summary>
    /// <param name="bytes">
    /// The canonical source bytes being inspected.
    /// </param>
    /// <param name="position">
    /// The byte position to inspect.
    /// </param>
    /// <returns>
    /// True when <c>*/</c> begins at the specified position.
    /// </returns>
    private static bool IsBlockCommentEnd(ReadOnlySpan<byte> bytes, int position)
        => position + 1 < bytes.Length && bytes[position] == (byte)'*' && bytes[position + 1] == (byte)'/';
}