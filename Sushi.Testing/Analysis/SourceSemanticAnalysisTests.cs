using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Semantics;

namespace Sushi.Testing.Analysis;

[TestFixture]
public class SourceSemanticAnalysisTests
{
    private static readonly Uri testUri = new("file:///TestProject/Semantics.sus");

    [TestCase(TestName = "Source Analyzer Should Bind Package Identity")]
    public async Task SourceAnalyzerShould_0()
    {
        const string source = """
         package Sushi.StandardLibrary.Text;
         namespace Sushi.Text;
         """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.StandardLibrary.Text");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Remove Identifier Escape From Package Identity")]
    public async Task SourceAnalyzerShould_1()
    {
        const string source = """
            package Sushi.@if;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.if");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should not Bind Package Identity With Missing Name Component")]
    public async Task SourceAnalyzerShould_2()
    {
        const string source = """
            package Sushi..Text;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Source Analyzer Should Bind Recovered Package Identity Across Adjacency Violation")]
    public async Task SourceAnalyzerShould_3()
    {
        const string source = """
            package Sushi . Text;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency))
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should not Bind Package Identity With Missing Semicolon")]
    public async Task SourceAnalyzerShould_4()
    {
        const string source = """
            package Sushi.Text
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Source Analyzer Should Not Manufacture Package Identity Without Package Declaration")]
    public async Task SourceAnalyzerShould_5()
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
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingPackageDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Package Identity When Later Source Is Not Yet Parsed")]
    public async Task SourceAnalyzerShould_6()
    {
        const string source = """
            package Sushi.Text;
            namespace Sushi.Text;
            using Sushi.Other;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> _) = await AnalysisHelper.AnalyzeSource(source, testUri);

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
        const string source = 
            """
            package Sushi.StandardLibrary.Text;
            namespace Sushi.Text;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Package!.QualifiedName
            .Should()
            .Be("Sushi.StandardLibrary.Text");

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.Text");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Remove Identifier Escape From Namespace Identity")]
    public async Task SourceAnalyzerShould_8()
    {
        const string source = 
            """
            package Test;
            namespace Sushi.@if;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Namespace!.QualifiedName
            .Should()
            .Be("Sushi.if");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Missing Namespace Declaration")]
    public async Task SourceAnalyzerShould_9()
    {
        const string source = "package Test;";

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Namespace
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingNamespaceDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Duplicate Namespace Declaration")]
    public async Task SourceAnalyzerShould_10()
    {
        const string source = 
            """
            package Test;
            namespace Test.One;
            namespace Test.Two;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Namespace
            .Should()
            .BeNull();

        result.Package
            .Should()
            .NotBeNull();

        result.Package.QualifiedName!
            .Should()
            .Be("Test");

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.DuplicateNamespaceDeclaration));
    }

    [TestCase(TestName = "Source Analyzer Should Diagnose Each Namespace Declaration Beyond The First")]
    public async Task SourceAnalyzerShould_11()
    {
        const string source = 
            """
            package Test;
            namespace Test.One;
            namespace Test.Two;
            namespace Test.Three;
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.DuplicateNamespaceDeclaration))
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Source Analyzer Should Bind Minimum Int32 Function")]
    public async Task SourceAnalyzerShould_12()
    {
        const string source = 
            """
            package Example;
            namespace Example;

            public int32 main() {
                return 42;
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        diagnostics
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
        const string source = 
            """
            package Example;
            namespace Example;

            public int32 main() {
                return x#2A;
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        diagnostics
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

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(source, testUri);

        result.Functions
            .Should()
            .BeEmpty();

        SushiDiagnostic diagnostic = diagnostics
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.IsType(ErrorType.IntegerConstantOutOfRange)
            .Should()
            .BeTrue();

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
}