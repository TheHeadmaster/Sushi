using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Performs lexical analysis of Sushi source text.
/// </summary>
public sealed class SourceLexer
{
    private const string UnterminatedBlockCommentCode = "SUSE001";

    public LexerResult Lex(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        List<SushiDiagnostic> diagnostics = [];

        int position = 0;

        while (position < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsLineCommentStart(bytes, position))
            {
                position = SkipLineComment(bytes, position);
                continue;
            }

            if (IsBlockCommentStart(bytes, position))
            {
                position = ScaneBlockComment(snapshot, bytes, position, diagnostics, cancellationToken);
                continue;
            }

            position++;
        }

        return new LexerResult(snapshot, diagnostics);
    }

    private static bool IsLineCommentStart(ReadOnlySpan<byte> bytes, int position)
        => position + 1 < bytes.Length
        && bytes[position] == (byte)'/'
        && bytes[position + 1] == (byte)'/';

    private static bool IsBlockCommentStart(ReadOnlySpan<byte> bytes, int position)
        => position + 1 < bytes.Length
        && bytes[position] == (byte)'/'
        && bytes[position + 1] == (byte)'*';

    private static bool IsBlockCommentEnd(ReadOnlySpan<byte> bytes, int position)
        => position + 1 < bytes.Length
        && bytes[position] == (byte)'*'
        && bytes[position + 1] == (byte)'/';

    private static int SkipLineComment(ReadOnlySpan<byte> bytes, int position)
    {
        position += 2;

        while (position < bytes.Length)
        {
            if (bytes[position] is (byte)'\r' or (byte)'\n')
            {
                break;
            }

            position++;
        }

        return position;
    }

    private static int ScaneBlockComment(SourceSnapshot snapshot, ReadOnlySpan<byte> bytes, int position, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        int commentStart = position;
        int depth = 1;

        position += 2;

        while (position < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsBlockCommentStart(bytes, position))
            {
                depth++;
                position += 2;

                continue;
            }

            if (IsBlockCommentEnd(bytes, position))
            {
                depth --;
                position += 2;

                if (depth == 0)
                {
                    return position;
                }

                continue;
            }

            position++;
        }

        diagnostics.Add(
            new SushiDiagnostic(
                UnterminatedBlockCommentCode,
                "Unterminated block comment.",
                DiagnosticSeverity.Error,
                new SourceSpan(snapshot, commentStart, commentStart + 2)
            )
        );

        return position;
    }
}