using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates lexical, syntactic, and initial semantic analysis of Sushi
/// source snapshots, including a signature-first project compilation mode.
/// </summary>
public sealed class SourceAnalyzer : Analyzer
{
    private readonly SourceLexer lexer = new();

    /// <summary>
    /// Analyzes a source snapshot and binds all supported declarations and bodies.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives analysis diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels analysis.
    /// </param>
    /// <returns>
    /// The analyzed syntax and complete supported semantic declarations.
    /// </returns>
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
        => this.AnalyzeCore(snapshot, diagnosticReporter, signatureOnly: false, cancellationToken);

    /// <summary>
    /// Analyzes syntax, package identity, activity declarations, and function
    /// signatures without binding bodies before project-wide name resolution.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives lexical, syntactic, and signature diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels analysis.
    /// </param>
    /// <returns>
    /// Declarations ready for the project-wide function-body binding pass.
    /// </returns>
    internal Task<AnalysisResult> AnalyzeDeclarations(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
        => this.AnalyzeCore(snapshot, diagnosticReporter, signatureOnly: true, cancellationToken);

    private Task<AnalysisResult> AnalyzeCore(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, bool signatureOnly, CancellationToken cancellationToken)
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
        SourceFileBindResult bindResult = SourceFileBinder.Bind(sourceFile, localDiagnostics, signatureOnly, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        diagnosticReporter.CommitReporter(localDiagnostics);

        SourceAnalysisResult result = new(snapshot, parserResult.Tree, bindResult.Package, bindResult.Namespace, bindResult.Functions)
        {
            Activities = bindResult.Activities
        };

        return Task.FromResult<AnalysisResult>(result);
    }
}