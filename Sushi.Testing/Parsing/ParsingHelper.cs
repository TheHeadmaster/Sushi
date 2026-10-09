using FluentAssertions;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

public static class ParsingHelper
{
    public static (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) ParsePackageDeclaration(string source, Uri uri)
    {
        LexerResult lexerResult = Lex(source, uri);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ParserResult result = SourceParser.ParsePackageDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) ParseNamespaceDeclaration(string source, Uri uri)
    {
        LexerResult lexerResult = Lex(source, uri);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ParserResult result = SourceParser.ParseNamespaceDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) ParseFunctionDeclaration(string source, Uri uri)
    {
        LexerResult lexerResult = Lex(source, uri);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ParserResult result = SourceParser.ParseFunctionDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) ParseSourceFile(string source, Uri uri)
    {
        LexerResult lexerResult = Lex(source, uri);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ParserResult result = SourceParser.ParseSourceFile(lexerResult, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    private static LexerResult Lex(string source, Uri uri)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(uri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceLexer lexer = new();

        LexerResult result = lexer.Lex(snapshot, diagnosticReporter, CancellationToken.None);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return result;
    }
}