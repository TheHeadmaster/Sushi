using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public sealed class ParameterInlayHintSemanticTests
{
    [Test]
    public async Task BindingShouldCaptureNestedAndNamedArgumentParameterMappings()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Add(int32 left, int32 right) { return left + right; }
            public void Place(int32 x, int32 y) {}
            public int32 Start(int32 x) { Place(y: 20, x: 10); return Add(x, Add(1, 2)); }
            """;
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///ParameterHints.sus"), 1, source);
        IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync([snapshot], null, CancellationToken.None);
        SourceAnalysisResult analysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>().Single();

        analysis.ParameterHints.Should().HaveCount(6)
            .And.OnlyContain(item => ReferenceEquals(item.ArgumentSpan.Snapshot, snapshot));
        analysis.ParameterHints.Select(item => item.Parameter.Name).Should().Equal("y", "x", "left", "right", "left", "right");
        analysis.ParameterHints.Take(2).Should().OnlyContain(item => item.IsNamedArgument);
        analysis.ParameterHints.Skip(2).Should().OnlyContain(item => !item.IsNamedArgument);
        analysis.ParameterHints[2].IsDirectIdentifierMatch.Should().BeTrue();
        analysis.ParameterHints[3].IsDirectIdentifierMatch.Should().BeFalse();
        analysis.ParameterHints[4].ArgumentSpan.Start.Should().BeGreaterThan(analysis.ParameterHints[3].ArgumentSpan.Start);
        analysis.ParameterHints.Select(item => item.ArgumentSpan.Start).Should().BeInAscendingOrder();
    }

    [Test]
    public async Task FailedOrUnresolvedCallsShouldNotProduceParameterMappings()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Known(int32 value) {}
            public void Start() { Missing(1); }
            """;
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///UnresolvedParameterHints.sus"), 1, source);
        IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync([snapshot], null, CancellationToken.None);
        SourceAnalysisResult analysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>().Single();

        analysis.ParameterHints.Should().BeEmpty();
        results.Single().Reporter.ReportDiagnostics().Should().Contain(item => item.Severity is Sushi.Diagnostics.DiagnosticSeverity.Error);
    }
}
