using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public class SourceEncodingAnalysisTests
{
    [TestCase(TestName = "Project Analyzer Should Stop Before TOML Parsing For Malformed UTF-8")]
    public async Task ProjectAnalyzerShould_0()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("name = \"");

        byte[] source =
        [
            .. prefix,
            0xFF,
            (byte)'"'
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(new Uri("file:///TestProject/Test.susproj"), version: null, source);

        ProjectAnalyzer analyzer = new();

        AnalysisResult result = await analyzer.Analyze(snapshot, CancellationToken.None);

        ProjectAnalysisResult projectResult = result
            .Should()
            .BeOfType<ProjectAnalysisResult>()
            .Subject;

        projectResult.Project
            .Should()
            .BeNull();

        projectResult.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = projectResult.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Snapshot
            .Should()
            .BeSameAs(snapshot);

        diagnostic.Span.Start
            .Should()
            .Be(prefix.Length);

        diagnostic.Span.End
            .Should()
            .Be(prefix.Length + 1);
    }

    [TestCase(TestName = "Solution Analyzer Should Stop Before TOML Parsing For Malformed UTF-8")]
    public async Task SolutionAnalyzerShould_0()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("name = \"");

        byte[] source =
        [
            .. prefix,
            0xFF,
            (byte)'"'
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(new Uri("file:///TestProject/Test.susln"), version: null, source);

        SolutionAnalyzer analyzer = new();

        AnalysisResult result = await analyzer.Analyze(snapshot, CancellationToken.None);

        SolutionAnalysisResult solutionResult = result
            .Should()
            .BeOfType<SolutionAnalysisResult>()
            .Subject;

        solutionResult.Solution
            .Should()
            .BeNull();

        solutionResult.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = solutionResult.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Snapshot
            .Should()
            .BeSameAs(snapshot);

        diagnostic.Span.Start
            .Should()
            .Be(prefix.Length);

        diagnostic.Span.End
            .Should()
            .Be(prefix.Length + 1);
    }
}