using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Documentation;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public sealed class DocumentationScaffoldResolverTests
{
    private static readonly Uri testUri = new("file:///TestProject/Scaffold.sus");

    [Test]
    public async Task CallableScaffoldShouldPreserveParameterOrderSkipDiscardsAndIncludeReturn()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n    ///\n    public int32 Calculate(int32 left, int32 _, bool right) { return left; }");
        DocumentationScaffold? scaffold = DocumentationScaffoldResolver.Resolve(analysis, 1, 7, CancellationToken.None);

        scaffold.Should().NotBeNull();
        scaffold!.ParameterNames.Should().Equal("left", "right");
        scaffold.HasReturnDescription.Should().BeTrue();
    }

    [TestCase("public")]
    [TestCase("internal")]
    [TestCase("package")]
    [TestCase("private")]
    [TestCase("protected")]
    public async Task CallableScaffoldsShouldNotDependOnAccessibility(string access)
    {
        SourceAnalysisResult analysis = await Analyze($"package Example;\n///\n{access} void Run() {{}}");
        DocumentationScaffold? scaffold = DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None);

        scaffold.Should().NotBeNull();
        scaffold!.ParameterNames.Should().BeEmpty();
        scaffold.HasReturnDescription.Should().BeFalse();
    }

    [Test]
    public async Task EmptyNonVoidCallableShouldStillReceiveAReturnDescription()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n///\npublic int32 Run() { return 1; }");
        DocumentationScaffold? scaffold = DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None);

        scaffold.Should().NotBeNull();
        scaffold!.ParameterNames.Should().BeEmpty();
        scaffold.HasReturnDescription.Should().BeTrue();
    }

    [Test]
    public async Task ActivityScaffoldShouldContainDescriptionOnly()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n///\npublic activity Worker: persistent {}");
        DocumentationScaffold? scaffold = DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None);

        scaffold.Should().NotBeNull();
        scaffold!.ParameterNames.Should().BeEmpty();
        scaffold.HasReturnDescription.Should().BeFalse();
    }

    [Test]
    public async Task ExistingDocumentationShouldRemainUntouched()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n/// existing description\n///\npublic void Run() {}");

        DocumentationScaffoldResolver.Resolve(analysis, 2, 3, CancellationToken.None)
            .Should()
            .BeNull();
    }

    [Test]
    public async Task DetachedDocumentationShouldNotReceiveAScaffold()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n///\n\npublic void Run() {}");

        DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None)
            .Should()
            .BeNull();
    }

    [Test]
    public async Task IncompleteSignatureShouldNotReceiveAGuessedScaffold()
    {
        SourceAnalysisResult analysis = await Analyze("package Example;\n///\npublic int32 Run(int32 value { return value; }");

        DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None)
            .Should()
            .BeNull();
    }

    [Test]
    public async Task ResolverShouldSupportLfAndCrLfSourceCoordinates()
    {
        foreach (string lineEnding in new[] { "\n", "\r\n" })
        {
            SourceAnalysisResult analysis = await Analyze($"package Example;{lineEnding}///{lineEnding}public int32 Run() {{ return 1; }}");
            DocumentationScaffoldResolver.Resolve(analysis, 1, 3, CancellationToken.None)
                .Should()
                .NotBeNull();
        }
    }

    private static async Task<SourceAnalysisResult> Analyze(string source)
    {
        SourceAnalysisResult analysis = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(
            SourceSnapshot.FromText(testUri, 1, source), new Sushi.Diagnostics.DiagnosticReporter(), CancellationToken.None);
        return analysis;
    }
}