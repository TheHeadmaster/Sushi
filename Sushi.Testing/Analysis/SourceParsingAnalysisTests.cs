using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public class SourceParsingAnalysisTests
{
    private static readonly Uri testUri = new("file:///TestProject/Parsing.sus");

    [TestCase(TestName = "Source Analyzer Should Produce Source File For Valid Package And Namespace Declarations")]
    public async Task SourceAnalyzerShould_0()
    {
        const string source = """
         package Sushi.StandardLibrary.Text;
         namespace Sushi.Text;
         """;

         (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        PackageDeclarationSyntax declaration = sourceFile.PackageDeclarations[0];

        declaration.Name.Segments
            .Should()
            .HaveCount(3);

        declaration.Name.Separators
            .Should()
            .HaveCount(2);

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();

        sourceFile.Span.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);

        sourceFile.Span.Start
            .Should()
            .Be(0);

        sourceFile.Span.End
            .Should()
            .Be(source.Length);

        sourceFile.NamespaceDeclarations.Should()
            .ContainSingle();
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Trivia Without Treating It As Unparsed Syntax")]
    public async Task SourceAnalyzerShould_1()
    {
        const string source = """
            /*before*/
            package Sushi.Text;
            namespace Sushi.Text;
             /*after*/
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        diagnostics
            .Should()
            .BeEmpty();

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();

        sourceFile.Span.End
            .Should()
            .Be(source.Length);

        result.SyntaxTree.LexerResult.Tokens
            .Count(token => token.Type is LexTokenType.BlockComment)
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should Retain Source Beyond Implemented Grammar")]
    public async Task SourceAnalyzerShould_2()
    {
        const string source = """
            package Sushi.Text;
            namespace Sushi.Text;
            using Sushi.Other;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        diagnostics
            .Should()
            .BeEmpty();

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        SourceSpan remainder = sourceFile.UnparsedContentSpan.Value;

        remainder.Start
            .Should()
            .Be(source.IndexOf("using", StringComparison.Ordinal));

        remainder.End
            .Should()
            .Be(source.Length);

        remainder.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);
    }

    [TestCase(TestName = "Source Analyzer Should Aggregate Lexical And Syntactic Diagnostics")]
    public async Task SourceAnalyzerShould_3()
    {
        const string source = """
            package Sushi . Text;
            namespace Sushi.Text;
             42foo
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.MissingLexicalSeparation))
            .Should()
            .Be(1);

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency))
            .Should()
            .Be(2);

        diagnostics
            .Should()
            .HaveCount(3);

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        PackageDeclarationSyntax declaration = sourceFile.PackageDeclarations[0];

        declaration.Name.Segments
            .Should()
            .HaveCount(2);

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(source.IndexOf("42foo", StringComparison.Ordinal));
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Structural Recovery In Source File")]
    public async Task SourceAnalyzerShould_4()
    {
        const string source = """
            package Sushi..Text;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        IReadOnlyList<PackageDeclarationSyntax> declarations = sourceFile.PackageDeclarations;

        declarations
            .Should()
            .ContainSingle();

        PackageDeclarationSyntax declaration = declarations[0];

        declaration.Name.Segments
            .Should()
            .HaveCount(3);

        declaration.Name.Segments[1].IsMissing
            .Should()
            .BeTrue();

        declaration.Name.Separators
            .Should()
            .HaveCount(2);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax))
            .And
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency));

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Unexpected Leading Content Without Speculative Synchronization")]
    public async Task SourceAnalyzerShould_5()
    {
        const string source = "using Sushi.Text;";

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        sourceFile.PackageDeclarations
            .Should()
            .BeEmpty();

        sourceFile.NamespaceDeclarations
            .Should()
            .BeEmpty();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic =>  diagnostic.IsType(ErrorType.MissingPackageDeclaration))
            .And.ContainSingle(diagnostic =>  diagnostic.IsType(ErrorType.MissingNamespaceDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Aggregate Encoding Diagnostics And Preserve The Source Tree")]
    public async Task SourceAnalyzerShould_6()
    {
        byte[] source =
        [
            ..
            Encoding.UTF8.GetBytes("package Sushi.Text;\nnamespace Sushi.Text;"), 0xFF
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(snapshot);   

        SourceFileSyntax sourceFile = AnalysisHelper.GetSourceFile(result);

        result.SyntaxTree.Snapshot
            .Should()
            .BeSameAs(snapshot);

        diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Span.Start == source.Length - 1
                && diagnostic.Span.End == source.Length);

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Missing Package Declaration")]
    public async Task SourceAnalyzerShould_7()
    {
        const string source = "namespace Sushi.Text;";

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        result.Package
            .Should()
            .BeNull();

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        diagnostics
            .Should()
            .ContainSingle(diagnostic =>  diagnostic.IsType(ErrorType.MissingPackageDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Duplicate Package Declaration")]
    public async Task SourceAnalyzerShould_8()
    {
        const string source =
            """
            package Sushi.Text;
            package Sushi.Other;
            namespace Sushi.Text;
            """
        ;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);   

        result.Package
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.DuplicatePackageDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Each Package Declaration Beyond The First")]
    public async Task SourceAnalyzerShould_9()
    {
        const string source =
            """
            package Sushi.One;
            package Sushi.Two;
            package Sushi.Three;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);  

        result.Package
            .Should()
            .BeNull();

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.DuplicatePackageDeclaration))
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Missing Package At End of Empty Source")]
    public async Task SourceAnalyzerShould_10()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(string.Empty, testUri);  

        result.Package
            .Should()
            .BeNull();

        SushiDiagnostic diagnosticA = diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingPackageDeclaration))
            .Which;

        SushiDiagnostic diagnosticB = diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingNamespaceDeclaration))
            .Which;

        diagnosticA.Span.Start
            .Should()
            .Be(0);

        diagnosticA.Span.End
            .Should()
            .Be(0);

        diagnosticB.Span.Start
            .Should()
            .Be(0);

        diagnosticB.Span.End
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Source Analyzer Should Report Encoding Diagnostics Before Later Pipeline Diagnostics")]
    public async Task SourceAnalyzerShould_11()
    {
        byte[] source = [
            .. Encoding.UTF8.GetBytes(
                """
                package Sushi . Text;
                namespace Sushi.Text;
                """
            ),
            0xFF
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(snapshot);

        diagnostics
            .Should()
            .NotBeEmpty();

        diagnostics[0].IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeTrue();

        diagnostics.Count(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency))
            .Should()
            .Be(2);
    }
}