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

    private readonly SourceParser parser = new();

    private readonly SourceFileBinder sourceFileBinder = new();

    /// <summary>
    /// Analyzes a Sushi source snapshot and produces its concrete syntax tree
    /// together with source-encoding, lexical, syntactic, and initial semantic diagnostics.
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
    /// The source analysis result, including the recovered concrete syntax tree and combined diagnostics.
    /// </returns>
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        LexerResult lexerResult = this.lexer.Lex(snapshot, diagnosticReporter, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ParserResult parserResult = this.parser.ParseSourceFile(lexerResult, diagnosticReporter, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SourceFileSyntax sourceFile = (SourceFileSyntax)parserResult.Tree.Root;

        SourceFileBindResult bindResult = this.sourceFileBinder.Bind(sourceFile, diagnosticReporter, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        AccumulateEncodingDiagnostics(diagnosticReporter, snapshot);

        return Task.FromResult<AnalysisResult>(new SourceAnalysisResult(snapshot, parserResult.Tree, bindResult.Package, bindResult.Namespace, bindResult.Functions));
    }
}