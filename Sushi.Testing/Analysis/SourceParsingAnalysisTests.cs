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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceAnalysisResult result = await Analyze(source, diagnosticReporter);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        SourceFileSyntax sourceFile = GetSourceFile(result);

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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceAnalysisResult result = await Analyze(source, diagnosticReporter);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceAnalysisResult result = await Analyze(source, diagnosticReporter);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceAnalysisResult result = await Analyze(source, diagnosticReporter);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        diagnostics
            .Count(diagnostic => diagnostic.Code == 0)
            .Should()
            .Be(1);

        diagnostics
            .Count(diagnostic => diagnostic.Code == 0)
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

    /*

    [TestCase(TestName = "Source Analyzer Should Preserve Structural Recovery In Source File")]
    public async Task SourceAnalyzerShould_4()
    {
        const string source = """
            package Sushi..Text;
            namespace Sushi.Text;
            """;

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

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

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE006")
            .And
            .NotContain(diagnostic => diagnostic.Code == "SUSE007");

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Unexpected Leading Content Without Speculative Synchronization")]
    public async Task SourceAnalyzerShould_5()
    {
        const string source = "using Sushi.Text;";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

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

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE008")
            .And.ContainSingle(diagnostic => diagnostic.Code == "SUSE010");
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

        SourceAnalysisResult result = await Analyze(snapshot);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        result.SyntaxTree.Snapshot
            .Should()
            .BeSameAs(snapshot);

        result.Diagnostics
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
        SourceAnalysisResult result = await Analyze("namespace Sushi.Text;");

        result.Package
            .Should()
            .BeNull();

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE008");
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Duplicate Package Declaration")]
    public async Task SourceAnalyzerShould_8()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Sushi.Text;
            package Sushi.Other;
            namespace Sushi.Text;
            """
        );

        result.Package
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE009");
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Each Package Declaration Beyond The First")]
    public async Task SourceAnalyzerShould_9()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Sushi.One;
            package Sushi.Two;
            package Sushi.Three;
            namespace Sushi.Text;
            """
        );

        result.Package
            .Should()
            .BeNull();

        result.Diagnostics
            .Count(diagnostic => diagnostic.Code == "SUSE009")
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Missing Package At End of Empty Source")]
    public async Task SourceAnalyzerShould_10()
    {
        SourceAnalysisResult result = await Analyze(string.Empty);

        result.Package
            .Should()
            .BeNull();

        SushiDiagnostic diagnosticA = result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE008")
            .Which;

        SushiDiagnostic diagnosticB = result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE010")
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
    */

    private static SourceFileSyntax GetSourceFile(SourceAnalysisResult result)
        => result.SyntaxTree.Root
                .Should()
                .BeOfType<SourceFileSyntax>().Subject;

    private static Task<SourceAnalysisResult> Analyze(string source, IDiagnosticReporter diagnosticReporter)
        => Analyze(SourceSnapshot.FromText(testUri, version: null, source), diagnosticReporter);

    private static async Task<SourceAnalysisResult> Analyze(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter)
    {
        SourceAnalyzer analyzer = new();

        AnalysisResult result = await analyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        return result
            .Should()
            .BeOfType<SourceAnalysisResult>().Subject;
    }
}