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

        (ProjectAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeProject(snapshot);
        
        result.Project
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeTrue();
        
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

        (SolutionAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSolution(snapshot);

        result.Solution
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeTrue();

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