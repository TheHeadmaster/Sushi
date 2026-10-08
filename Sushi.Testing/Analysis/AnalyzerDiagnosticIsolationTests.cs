using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public class AnalyzerDiagnosticIsolationTests
{
    [TestCase(TestName = "Project Analyzer Should Ignore Preexisting Caller Errors When Producing Project")]
    public async Task ProjectAnalyzerShould_0()
    {
        const string source = """
        name = "Test"
        assembly = "Test"
        language-version = "1.0"
        
        [build]
        default = "debug"
        sources = ["Build/**/*.sus"]

        [build.targets.debug]
        type = "Sushi.MVP.Debug"
        """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///TestProject/Test.susproj"), version: null, source);

        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        diagnosticReporter.GenerateError(ErrorType.ExpectedSyntax, new SourceSpan(snapshot, 0, 0));

        AnalysisResult result = await ProjectAnalyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        ProjectAnalysisResult projectResult = result
            .Should()
            .BeOfType<ProjectAnalysisResult>()
            .Subject;

        projectResult.Project
            .Should()
            .NotBeNull();

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        SushiDiagnostic diagnostic = diagnostics
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.IsType(ErrorType.ExpectedSyntax)
            .Should()
            .BeTrue();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }
}