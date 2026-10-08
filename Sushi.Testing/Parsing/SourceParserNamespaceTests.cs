using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class SourceParserNamespaceTests
{
    private static readonly Uri testUri = new("file:///TestProject/Namespace.sus");

    [TestCase(TestName = "Parser Should Parse Dotted Namespace Declaration")]
    public void ParserShould_0()
    {
        const string source = "namespace Sushi.StandardLibrary.Text;";

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ParserResult result = Parse(source, diagnosticReporter);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        NamespaceDeclarationSyntax declaration = GetDeclaration(result);

        declaration.NamespaceKeyword.Type
            .Should()
            .Be(SyntaxType.NamespaceKeyword);

        declaration.NamespaceKeyword.IsMissing
            .Should()
            .BeFalse();

        declaration.Name.Segments
            .Select(segment => segment.Type)
            .Should()
            .Equal(
                SyntaxType.IdentifierToken,
                SyntaxType.IdentifierToken,
                SyntaxType.IdentifierToken
            );

        declaration.Name.Separators
            .Select(separator => separator.Type)
            .Should()
            .Equal(
                SyntaxType.DotToken,
                SyntaxType.DotToken
            );

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        declaration.Span.Snapshot
            .Should()
            .BeSameAs(result.Tree.Snapshot);

        declaration.Span.Start
            .Should()
            .Be(0);

        declaration.Span.End
            .Should()
            .Be(source.Length);
    }

    [TestCase("namespace Sushi.Text;", 0, TestName = "Parser Should Accept Adjacent Namespace Name")]
    [TestCase("namespace Sushi .Text;", 1, TestName = "Parser Should Diagnose Trivia Before Namespace Dot")]
    [TestCase("namespace Sushi. Text;", 1, TestName = "Parser Should Diagnose Trivia After Namespace Dot")]
    [TestCase("namespace Sushi . Text;", 2, TestName = "Parser Should Diagnose Each Violated Namespace Dot Boundary")]
    public void ParserShould_1(string source, int expectedDiagnostics)
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ParserResult result = Parse(source, diagnosticReporter);

        NamespaceDeclarationSyntax declaration = GetDeclaration(result);

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        diagnostics
            .Count(diagnostic => diagnostic.Code == 0)
            .Should()
            .Be(expectedDiagnostics);

        diagnostics
            .Count(diagnostic => diagnostic.Code == 0)
            .Should()
            .Be(0);

        declaration.Name.Segments
            .Should()
            .HaveCount(2)
            .And
            .OnlyContain(segment => segment.IsSourceBacked);

        declaration.Name.Separators
            .Should()
            .ContainSingle(separator => separator.IsSourceBacked);

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Accept Escaped Namespace Name Component")]

    public void ParserShould_2()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ParserResult result = Parse("namespace Sushi.@if;", diagnosticReporter);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        NamespaceDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments
            .Should()
            .HaveCount(2);

        declaration.Name.Segments[1].Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);
    }

    [TestCase("namespace .Text;", 0, 2, 1, TestName = "Parser Should Synthesize Missing Initial Namespace Name")]
    [TestCase("namespace Sushi.;", 1, 2, 1, TestName = "Parser Should Synthesize Missing Terminal Namespace Name")]
    [TestCase("namespace Sushi..Text;", 1, 3, 2, TestName = "Parser Should Synthesize Namespace Name Between Consecutive Dots")]
    public void ParserShould_3(string source, int missingIndex, int expectedSegments, int expectedSeparators)
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ParserResult result = Parse(source, diagnosticReporter);
        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();
        NamespaceDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments
            .Should()
            .HaveCount(expectedSegments);

        declaration.Name.Segments[missingIndex].IsMissing
            .Should()
            .BeTrue();

        declaration.Name.Segments
            .Count(segment => segment.IsMissing)
            .Should()
            .Be(1);

        declaration.Name.Separators
            .Should()
            .HaveCount(expectedSeparators);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == 0)
            .And
            .NotContain(diagnostic => diagnostic.Code == 0);

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Synthesize Missing Namespace Semicolon")]
    public void ParserShould_4()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ParserResult result = Parse("namespace Sushi.Text", diagnosticReporter);
        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        NamespaceDeclarationSyntax declaration = GetDeclaration(result);

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeTrue();

        declaration.SemicolonToken.Type
            .Should()
            .Be(SyntaxType.SemicolonToken);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == 0);
    }

    private static NamespaceDeclarationSyntax GetDeclaration(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<NamespaceDeclarationSyntax>()
            .Subject;

    private static ParserResult Parse(string source, IDiagnosticReporter diagnosticReporter)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        LexerResult lexerResult = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        return new SourceParser().ParseNamespaceDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);
    }
}