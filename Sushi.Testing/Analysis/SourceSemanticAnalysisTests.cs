using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
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
        SourceAnalysisResult result = await Analyze("package Sushi.@if;");

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
        SourceAnalysisResult result = await Analyze("package Sushi..Text;");

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
        SourceAnalysisResult result = await Analyze("package Sushi . Text;");

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
        SourceAnalysisResult result = await Analyze("package Sushi.Text");

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
    }

    [TestCase(TestName = "Source Analyzer Should Preserve Package Identity When Later Source Is Not Yet Parsed")]
    public async Task SourceAnalyzerShould_6()
    {
        SourceAnalysisResult result = await Analyze("package Sushi.Text;\nnamespace Sushi.Text;");

        result.Package
            .Should()
            .NotBeNull();

        result.Package!.QualifiedName
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
            .BeNull();

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