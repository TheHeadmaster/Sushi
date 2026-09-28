using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Performs lexical analysis of Sushi source text.
/// </summary>
public sealed class SourceLexer : Lexer
{
    private const string UnterminatedBlockCommentCode = "SUSE001";

    private static readonly ILexTokenizer[] tokenizers =
    [
        new CommentTokenizer(),
        new WhitespaceTokenizer()
    ];

    public override LexerResult Lex(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        List<SushiDiagnostic> diagnostics = [];

        int position = 0;

        while (position < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexTokenMatch? bestMatch = null;

            foreach (ILexTokenizer tokenizer in tokenizers)
            {
                if (!tokenizer.CanStart(bytes[position]))
                {
                    continue;
                }
        
                if (!tokenizer.TryRecognize(snapshot, position, cancellationToken, out LexTokenMatch candidate))
                {
                    continue;
                }
        
                if (bestMatch is null || candidate.Length > bestMatch.Value.Length)
                {
                    bestMatch = candidate;
                    continue;
                }
        
                if (candidate.Length == bestMatch.Value.Length && candidate.Type != bestMatch.Value.Type)
                {
                    throw new InvalidOperationException("Ambiguous lexical tokenization.");
                }
            }

            position++;
        }

        return new LexerResult(snapshot, tokens, diagnostics);
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

    private static int ScanBlockComment(SourceSnapshot snapshot, ReadOnlySpan<byte> bytes, int position, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
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