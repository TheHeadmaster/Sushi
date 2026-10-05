using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates lexical and syntactic analysis for a Sushi source snapshot.
/// </summary>
public sealed class SourceAnalyzer
{
    private readonly SourceLexer lexer = new();

    private readonly SourceParser parser = new();

    private readonly PackageDeclarationBinder packageBinder = new();

    /// <summary>
    /// Analyzes a Sushi source snapshot and produces its concrete syntax tree
    /// together with source-encoding, lexical, and syntactic diagnostics.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot to analyze.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel analysis.
    /// </param>
    /// <returns>
    /// The source analysis result, including the recovered concrete syntax tree and combined diagnostics.
    /// </returns>
    public Task<AnalysisResult> Analyze(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        LexerResult lexerResult = this.lexer.Lex(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ParserResult parserResult = this.parser.ParseSourceFile(lexerResult, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SourceFileSyntax sourceFile = (SourceFileSyntax)parserResult.Tree.Root;

        PackageIdentity? package = sourceFile.PackageDeclaration is null ? null : this.packageBinder.Bind(sourceFile.PackageDeclaration, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SushiDiagnostic[] diagnostics =
        [
            .. SourceEncodingDiagnostics.Create(snapshot),
            .. lexerResult.Diagnostics,
            .. parserResult.Diagnostics
        ];

        return Task.FromResult<AnalysisResult>(new SourceAnalysisResult(snapshot, diagnostics, parserResult.Tree, package));
    }
}