using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

public static class AnalysisHelper
{
    public static Task<(ProjectAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> AnalyzeProject(string source, Uri uri)
        => AnalyzeProject(SourceSnapshot.FromText(uri, version: null, source));

    public static async Task<(ProjectAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> AnalyzeProject(SourceSnapshot snapshot)
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        AnalysisResult analysisResult = await ProjectAnalyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        ProjectAnalysisResult result = analysisResult
            .Should()
            .BeOfType<ProjectAnalysisResult>()
            .Subject;

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static Task<(SolutionAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> AnalyzeSolution(string source, Uri uri)
        => AnalyzeSolution(SourceSnapshot.FromText(uri, version: null, source));

    public static async Task<(SolutionAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> AnalyzeSolution(SourceSnapshot snapshot)
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        AnalysisResult analysisResult = await SolutionAnalyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        SolutionAnalysisResult result = analysisResult
            .Should()
            .BeOfType<SolutionAnalysisResult>()
            .Subject;

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static Task<(SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> AnalyzeSource(string source, Uri uri)
        => AnalyzeSource(SourceSnapshot.FromText(uri, version: null, source));

    public static async Task<(SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> AnalyzeSource(SourceSnapshot snapshot)
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceAnalyzer analyzer = new();

        AnalysisResult analysisResult = await analyzer.Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        SourceAnalysisResult result = analysisResult
            .Should()
            .BeOfType<SourceAnalysisResult>()
            .Subject;

        return (result, diagnosticReporter.ReportDiagnostics());
    }

    public static SourceFileSyntax GetSourceFile([NotNull] SourceAnalysisResult result)
        => result.SyntaxTree.Root
                .Should()
                .BeOfType<SourceFileSyntax>().Subject;
}