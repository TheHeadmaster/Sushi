using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates lexical, syntactic, and initial semantic analysis for a Sushi source snapshot.
/// </summary>
public sealed class SourceAnalyzer : Analyzer
{
    private readonly SourceLexer lexer = new();

    /// <summary>
    /// Analyzes a Sushi source snapshot and produces its concrete syntax tree.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot to analyze.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel analysis.
    /// </param>
    /// <returns>
    /// The source analysis result, including the recovered concrete syntax tree.
    /// </returns>
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

        AccumulateEncodingDiagnostics(localDiagnostics, snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        LexerResult lexerResult = this.lexer.Lex(snapshot, localDiagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ParserResult parserResult = SourceParser.ParseSourceFile(lexerResult, localDiagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SourceFileSyntax sourceFile = (SourceFileSyntax)parserResult.Tree.Root;

        SourceFileBindResult bindResult = SourceFileBinder.Bind(sourceFile, localDiagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        diagnosticReporter.CommitReporter(localDiagnostics);

        return Task.FromResult<AnalysisResult>(new SourceAnalysisResult(snapshot, parserResult.Tree, bindResult.Package, bindResult.Namespace, bindResult.Functions));
    }
}