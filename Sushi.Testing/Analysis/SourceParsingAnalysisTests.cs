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

    [TestCase(TestName = "Source Analyzer Should Produce Source File For Valid Package Declaration")]
    public async Task SourceAnalyzerShould_0()
    {
        const string source = "package Sushi.StandardLibrary.Text;";

        SourceAnalysisResult result = await Analyze(source);

        result.Diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.PackageDeclaration
            .Should()
            .NotBeNull();

        sourceFile.PackageDeclaration!.Name.Segments
            .Should()
            .HaveCount(3);

        sourceFile.PackageDeclaration!.Name.Separators
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
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Trivia Without Treating It As Unparsed Syntax")]
    public async Task SourceAnalyzerShould_1()
    {
        const string source = "/*before*/\npackage Sushi.Text; /*after*/";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        result.Diagnostics
            .Should()
            .BeEmpty();

        sourceFile.PackageDeclaration
            .Should()
            .NotBeNull();

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
        const string source = "package Sushi.Text;\nnamespace Sushi.Text;";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        result.Diagnostics
            .Should()
            .BeEmpty();

        sourceFile.PackageDeclaration
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        SourceSpan remainder = sourceFile.UnparsedContentSpan.Value;

        remainder.Start
            .Should()
            .Be(source.IndexOf("namespace", StringComparison.Ordinal));

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
        const string source = "package Sushi . Text; 42foo";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        result.Diagnostics
            .Count(diagnostic => diagnostic.Code == "SUSE005")
            .Should()
            .Be(1);

        result.Diagnostics
            .Count(diagnostic => diagnostic.Code == "SUSE007")
            .Should()
            .Be(2);

        result.Diagnostics
            .Should()
            .HaveCount(3);

        sourceFile.PackageDeclaration
            .Should()
            .NotBeNull();

        sourceFile.PackageDeclaration.Name.Segments
            .Should()
            .HaveCount(2);

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(source.IndexOf("42foo", StringComparison.Ordinal));
    }

    [TestCase(TestName = "Source Aalyzer Should Preserve Structural Recovery In Source File")]
    public async Task SourceAnalyzerShould_4()
    {
        const string source = "package Sushi..Text;";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        PackageDeclarationSyntax? declaration = sourceFile.PackageDeclaration;

        declaration
            .Should()
            .NotBeNull();

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
        const string source = "namespace Sushi.Text;";

        SourceAnalysisResult result = await Analyze(source);

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.PackageDeclaration
            .Should()
            .BeNull();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(0);

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Aggregate Encoding Diagnostics And Preserve The Source Tree")]
    public async Task SourceAnalyzerShould_6()
    {
        byte[] source =
        [
            ..
            Encoding.UTF8.GetBytes("package Sushi.Text;"), 0xFF
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

        sourceFile.PackageDeclaration
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();
    }

    private static SourceFileSyntax GetSourceFile(SourceAnalysisResult result)
        => result.SyntaxTree.Root
                .Should()
                .BeOfType<SourceFileSyntax>().Subject;

    private static Task<SourceAnalysisResult> Analyze(string source)
        => Analyze(SourceSnapshot.FromText(testUri, version: null, source));

    private static async Task<SourceAnalysisResult> Analyze(SourceSnapshot snapshot)
    {
        SourceAnalyzer analyzer = new();

        AnalysisResult result = await analyzer.Analyze(snapshot, CancellationToken.None);

        return result
            .Should()
            .BeOfType<SourceAnalysisResult>().Subject;
    }
}