using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public sealed class ParameterNamingDiagnosticsTests
{
    private const string Header = "package Example;\nnamespace Example;\n";
    private static readonly Uri testUri = new("file:///TestProject/ParameterNames.sus");

    [Test]
    public async Task RecognizedPlaceholderNamesShouldProduceOneWarningInSourceOrder()
    {
        string[] names = ["arg", "arg0", "arg1", "arg123", "param", "param0", "param2", "param99", "param123", "foo", "temp"];
        string parameters = string.Join(", ", names.Select(name => $"int32 {name}"));
        string source = Header + $"private void Run({parameters}) {{}}";
        (SourceSnapshot snapshot, SourceAnalysisResult analysis, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        SushiDiagnostic[] warnings = PlaceholderWarnings(diagnostics);

        warnings.Should().HaveCount(names.Length);
        warnings.Select(warning => warning.DisplayName)
            .Should()
            .OnlyContain(name => name == "SUSW0022");
        warnings.Select(warning => DecodeSpan(warning.Span))
            .Should()
            .Equal(names);
        warnings.Should().OnlyContain(warning => ReferenceEquals(warning.Span.Snapshot, snapshot));
        analysis.Functions.Should().ContainSingle();
    }

    [TestCase("value")]
    [TestCase("item")]
    [TestCase("data")]
    [TestCase("input")]
    [TestCase("element")]
    [TestCase("result")]
    [TestCase("object")]
    [TestCase("parameter")]
    [TestCase("argument")]
    [TestCase("temperature")]
    [TestCase("temporaryValue")]
    [TestCase("tmp")]
    [TestCase("bar")]
    [TestCase("baz")]
    [TestCase("x")]
    [TestCase("y")]
    [TestCase("i")]
    [TestCase("j")]
    [TestCase("arg_1")]
    [TestCase("param_2")]
    [TestCase("temp42")]
    [TestCase("foo2")]
    [TestCase("Arg1")]
    [TestCase("ARG1")]
    [TestCase("Param2")]
    [TestCase("arg999999999999999999999999999999999999")]
    public async Task OtherIdentifierSpellingsShouldNotProducePlaceholderWarnings(string name)
    {
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(Header + $"private void Run(int32 {name}) {{}}")).Diagnostics;

        PlaceholderWarnings(diagnostics).Should().BeEmpty();
    }

    [Test]
    public async Task EscapedNamesShouldUseTheirSemanticSpellingAndDiscardShouldRemainUnaffected()
    {
        const string source = Header + "private void Run(int32 @arg, int32 @param2, int32 _) {}";
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(source)).Diagnostics;
        SushiDiagnostic[] warnings = PlaceholderWarnings(diagnostics);

        warnings.Should().HaveCount(2);
        warnings.Select(warning => DecodeSpan(warning.Span))
            .Should()
            .Equal("@arg", "@param2");
        warnings.Select(warning => warning.DisplayName)
            .Should()
            .OnlyContain(name => name == "SUSW0022");
    }

    [TestCase("public")]
    [TestCase("internal")]
    [TestCase("package")]
    [TestCase("protected")]
    [TestCase("private")]
    public async Task PlaceholderWarningsShouldApplyAtEveryFunctionAccessibility(string access)
    {
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(Header + $"{access} void Run(int32 arg) {{}}")).Diagnostics;

        PlaceholderWarnings(diagnostics).Should().ContainSingle();
    }

    [Test]
    public async Task ActivityLifecycleMethodParametersShouldBeValidatedEvenOnNonpublicActivities()
    {
        const string source = Header + """
            internal activity Worker : persistent
            {
                public entry void Start(int32 arg) {}
                private stop void Finish(int32 temp) {}
            }
            """;
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(source)).Diagnostics;

        PlaceholderWarnings(diagnostics)
            .Select(warning => DecodeSpan(warning.Span))
            .Should()
            .Equal("arg", "temp");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PlaceholderWarningsShouldNotDependOnDocumentation(bool includeDocumentation)
    {
        string documentation = includeDocumentation
            ? "/// Applies the operation.\n/// @param arg The operation input.\n"
            : string.Empty;
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(
            Header + documentation + "private void Run(int32 arg) {}")).Diagnostics;

        PlaceholderWarnings(diagnostics).Should().ContainSingle();
    }

    [Test]
    public async Task WarningsShouldIdentifyExactNameTokensInTheCurrentSnapshot()
    {
        const string source = Header + "// café\nprivate void Run(int32 arg1, int32 param2) {}";
        (SourceSnapshot Snapshot, SourceAnalysisResult Analysis, IReadOnlyList<SushiDiagnostic> Diagnostics) analyzed = await Analyze(source, version: 7);
        SourceSnapshot snapshot = analyzed.Snapshot;
        IReadOnlyList<SushiDiagnostic> diagnostics = analyzed.Diagnostics;
        SushiDiagnostic[] warnings = PlaceholderWarnings(diagnostics);
        int[] expectedStarts = ["arg1", "param2"]
            .Select(name => ByteOffset(source, source.IndexOf(name, StringComparison.Ordinal)))
            .ToArray();

        warnings.Should().HaveCount(2);
        warnings.Select(warning => warning.Span.Start).Should().Equal(expectedStarts);
        warnings.Select(warning => warning.Span.End)
            .Should()
            .Equal(expectedStarts[0] + 4, expectedStarts[1] + 6);
        warnings.Should().OnlyContain(warning => ReferenceEquals(warning.Span.Snapshot, snapshot));
        warnings.Select(warning => warning.Severity).Should().OnlyContain(severity => severity == DiagnosticSeverity.Warning);
        warnings.Select(warning => warning.DisplayName).Should().OnlyContain(name => name == "SUSW0022");
    }

    [Test]
    public async Task FullSignatureAndProjectAnalysisShouldReportEachWarningOnce()
    {
        string source = Header + "private void Run(int32 arg, int32 param2) {}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: 4, source);
        SourceAnalyzer analyzer = new();
        DiagnosticReporter fullReporter = new();
        DiagnosticReporter signatureReporter = new();

        await analyzer.Analyze(snapshot, fullReporter, CancellationToken.None);
        await analyzer.AnalyzeDeclarations(snapshot, signatureReporter, CancellationToken.None);
        IReadOnlyList<ProjectDiagnosticResult> projectResults = await new ProjectDiagnosticAnalyzer()
            .AnalyzeAsync([snapshot], affectedUris: null, CancellationToken.None);

        PlaceholderWarnings(fullReporter.ReportDiagnostics()).Should().HaveCount(2);
        PlaceholderWarnings(signatureReporter.ReportDiagnostics()).Should().HaveCount(2);
        ProjectDiagnosticResult projectResult = projectResults.Should().ContainSingle().Subject;
        PlaceholderWarnings(projectResult.Reporter.ReportDiagnostics()).Should().HaveCount(2);
        projectResult.Analysis.Snapshot.Should().BeSameAs(snapshot);
        PlaceholderWarnings(projectResult.Reporter.ReportDiagnostics())
            .Should()
            .OnlyContain(warning => ReferenceEquals(warning.Span.Snapshot, snapshot));
    }

    [Test]
    public async Task MissingParameterNamesShouldNotProduceWarningsAndBodyErrorsShouldNotSuppressReliableNames()
    {
        const string source = Header + """
            private void Missing(int32) {}
            private void Broken(int32 arg) { return 1; }
            @
            """;
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(source)).Diagnostics;

        PlaceholderWarnings(diagnostics)
            .Select(warning => DecodeSpan(warning.Span))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("arg");
        diagnostics.Should().Contain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public async Task UnsupportedDeclarationFormsShouldNotProduceParameterWarnings()
    {
        const string source = Header + "internal struct Container { private void Run(int32 arg) {} }";
        IReadOnlyList<SushiDiagnostic> diagnostics = (await Analyze(source)).Diagnostics;

        PlaceholderWarnings(diagnostics).Should().BeEmpty();
    }

    [Test]
    public async Task ReanalysisShouldReportAgainstTheNewSnapshotWithoutCarryingOldWarnings()
    {
        const string source = Header + "private void Run(int32 arg) {}";
        (SourceSnapshot Snapshot, SourceAnalysisResult Analysis, IReadOnlyList<SushiDiagnostic> Diagnostics) originalAnalysis = await Analyze(source, version: 1);
        string editedSource = source.Replace("arg", "amount", StringComparison.Ordinal);
        (SourceSnapshot Snapshot, SourceAnalysisResult Analysis, IReadOnlyList<SushiDiagnostic> Diagnostics) editedAnalysis = await Analyze(editedSource, version: 2);
        SourceSnapshot original = originalAnalysis.Snapshot;
        IReadOnlyList<SushiDiagnostic> originalDiagnostics = originalAnalysis.Diagnostics;
        SourceSnapshot edited = editedAnalysis.Snapshot;
        IReadOnlyList<SushiDiagnostic> editedDiagnostics = editedAnalysis.Diagnostics;

        SushiDiagnostic originalWarning = PlaceholderWarnings(originalDiagnostics).Should().ContainSingle().Subject;
        originalWarning.Span.Snapshot.Should().BeSameAs(original);
        PlaceholderWarnings(editedDiagnostics).Should().BeEmpty();
        editedDiagnostics.Should().NotContain(diagnostic => ReferenceEquals(diagnostic.Span.Snapshot, original));
        edited.Version.Should().Be(2);
    }

    private static async Task<(SourceSnapshot Snapshot, SourceAnalysisResult Analysis, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string source, int version = 1)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version, source);
        (SourceAnalysisResult analysis, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalysisHelper.AnalyzeSource(snapshot);
        return (snapshot, analysis, diagnostics);
    }

    private static SushiDiagnostic[] PlaceholderWarnings(IReadOnlyList<SushiDiagnostic> diagnostics)
        => [.. diagnostics.Where(diagnostic => diagnostic.DisplayName == "SUSW0022")];

    private static string DecodeSpan(SourceSpan span)
        => Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);

    private static int ByteOffset(string source, int characterOffset)
        => Encoding.UTF8.GetByteCount(source.AsSpan(0, characterOffset));
}