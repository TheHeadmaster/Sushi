using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a source snapshot.
/// </summary>
public sealed class SourceAnalyzer
{
    private readonly SourceLexer lexer = new();

    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        LexerResult lexerResult = this.lexer.Lex(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

            SushiDiagnostic[] diagnostics =
            [
                .. SourceEncodingDiagnostics.Create(snapshot),
                .. lexerResult.Diagnostics
            ];

        return Task.FromResult<AnalysisResult>(new SourceAnalysisResult(snapshot, diagnostics));
    }
}