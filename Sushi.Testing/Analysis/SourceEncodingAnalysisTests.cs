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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        AnalysisResult result = await ProjectAnalyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        ProjectAnalysisResult projectResult = result
            .Should()
            .BeOfType<ProjectAnalysisResult>()
            .Subject;

        projectResult.Project
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = diagnostics.Single();

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

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        AnalysisResult result = await SolutionAnalyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        SolutionAnalysisResult solutionResult = result
            .Should()
            .BeOfType<SolutionAnalysisResult>()
            .Subject;

        solutionResult.Solution
            .Should()
            .BeNull();

        diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = diagnostics.Single();

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