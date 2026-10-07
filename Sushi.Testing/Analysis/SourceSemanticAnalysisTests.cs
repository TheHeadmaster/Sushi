using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public class SourceSemanticAnalysisTests
{
    private static readonly Uri testUri = new("file:///TestProject/Semantics.sus");

    [TestCase(TestName = "Source Analyzer Should Bind Package Identity")]
    public async Task SourceAnalyzerShould_0()
    {
        SourceAnalysisResult result = await Analyze("""
         package Sushi.StandardLibrary.Text;
         namespace Sushi.Text;
         """);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.StandardLibrary.Text");

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Remove Identifier Escape From Package Identity")]
    public async Task SourceAnalyzerShould_1()
    {
        SourceAnalysisResult result = await Analyze("""
            package Sushi.@if;
            namespace Sushi.Text;
            """);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.if");

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should not Bind Package Identity With Missing Name Component")]
    public async Task SourceAnalyzerShould_2()
    {
        SourceAnalysisResult result = await Analyze("""
            package Sushi..Text;
            namespace Sushi.Text;
            """);

        result.Package
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE006");
    }

    [TestCase(TestName = "Source Analyzer Should Bind Recovered Package Identity Across Adjacency Violation")]
    public async Task SourceAnalyzerShould_3()
    {
        SourceAnalysisResult result = await Analyze("""
            package Sushi . Text;
            namespace Sushi.Text;
            """);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        result.Diagnostics
            .Count(diagnostic => diagnostic.Code == "SUSE007")
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should not Bind Package Identity With Missing Semicolon")]
    public async Task SourceAnalyzerShould_4()
    {
        SourceAnalysisResult result = await Analyze("""
            package Sushi.Text
            namespace Sushi.Text;
            """);

        result.Package
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE006");
    }

    [TestCase(TestName = "Source Analyzer Should Not Manufacture Package Identity Without Package Declaration")]
    public async Task SourceAnalyzerShould_5()
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

    [TestCase(TestName = "Source Analyzer Should Preserve Package Identity When Later Source Is Not Yet Parsed")]
    public async Task SourceAnalyzerShould_6()
    {
        SourceAnalysisResult result = await Analyze("""
            package Sushi.Text;
            namespace Sushi.Text;
            using Sushi.Other;
            """);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        result.Namespace
            .Should()
            .NotBeNull();

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.Text");
    }

    [TestCase(TestName = "Source Analyzer Should Bind Package And Namespace Identities")]
    public async Task SourceAnalyzerShould_7()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Sushi.StandardLibrary.Text;
            namespace Sushi.Text;
            """
        );

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.StandardLibrary.Text");

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Remove Identifier Escape From Namespace Identity")]
    public async Task SourceAnalyzerShould_8()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Test;
            namespace Sushi.@if;
            """
        );

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.if");

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Missing Namespace Declaration")]
    public async Task SourceAnalyzerShould_9()
    {
        SourceAnalysisResult result = await Analyze("package Test;");

        result.Namespace
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE010");
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Duplicate Namespace Declaration")]
    public async Task SourceAnalyzerShould_10()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Test;
            namespace Test.One;
            namespace Test.Two;
            """
        );

        result.Namespace
            .Should()
            .BeNull();

        result.Package
            .Should()
            .NotBeNull();

        result.Package.QualifiedName!
            .Should()
            .Be("Test");

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE011");
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Each Namespace Declaration Beyond The First")]
    public async Task SourceAnalyzerShould_11()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Test;
            namespace Test.One;
            namespace Test.Two;
            namespace Test.Three;
            """
        );

        result.Diagnostics
            .Count(diagnostic => diagnostic.Code == "SUSE011")
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should Bind Minimum Int32 Function")]
    public async Task SourceAnalyzerShould_12()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Example;
            namespace Example;

            public int32 main() {
                return 42;
            }
            """
        );

        result.Diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        function.Accessibility
            .Should()
            .Be(Accessibility.Public);

        function.Name
            .Should()
            .Be("main");

        function.ReturnType
            .Should()
            .Be(BoundIntegerType.Int32);

        BoundReturnStatement returnStatement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerLiteralExpression expression = returnStatement.Expression
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;

        expression.Type
        .Should()
        .Be(BoundIntegerType.Int32);

        expression.Value
            .Should()
            .Be(42);
    }

    [TestCase(TestName = "Source Analyzer Should Bind Hexadecimal Integer Literal To Contextual Int32 Value")]
    public async Task SourceAnalyzerShould_13()
    {
        SourceAnalysisResult result = await Analyze(
            """
            package Example;
            namespace Example;

            public int32 main() {
                return x#2A;
            }
            """
        );

        result.Diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        BoundReturnStatement returnStatement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerLiteralExpression expression = returnStatement.Expression
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;

        expression.Type
            .Should()
            .Be(BoundIntegerType.Int32);

        expression.Value
            .Should()
            .Be(42);
    }

    [TestCase(TestName = "Source Analyzer Should Reject Integer Literal Outside Contextual Int32 Range")]
    public async Task SourceAnalyzerShould_14()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 main() {
                return 2_147_483_648;
            }
            """;

        SourceAnalysisResult result = await Analyze(source);

        result.Functions
            .Should()
            .BeEmpty();

        SushiDiagnostic diagnostic = result.Diagnostics
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.Code
            .Should()
            .Be("SUSE012");

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        int literalStart = source.IndexOf("2_147_483_648", StringComparison.Ordinal);

        diagnostic.Span.Start
            .Should()
            .Be(literalStart);

        diagnostic.Span.End
            .Should()
            .Be(literalStart + "2_147_483_648".Length);

        diagnostic.Span.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);
    }

    private static async Task<SourceAnalysisResult> Analyze(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        SourceAnalyzer analyzer = new();

        AnalysisResult result = await analyzer.Analyze(snapshot, CancellationToken.None);

        return result
            .Should()
            .BeOfType<SourceAnalysisResult>()
            .Subject;
    }
}