using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.LanguageServerProtocol;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public sealed class DocumentationReferenceResolutionTests
{
    [Test]
    public async Task InlineReferencesShouldUseCompilerCallableScopeAndDeclarationScopedParameters()
    {
        const string source = """
            package Example;
            namespace Example;
            /// {@link Calculate} and {@link Calculate()}.
            public int32 Calculate() { return 1; }
            /// {@link Calculate(int32)} and {@link Example.Example.Calculate(int32) | calculation}.
            public int32 Calculate(int32 amount) { return amount; }
            /// {@link Calculate}
            public int32 Ambiguous() { return 1; }
            private int32 Hidden() { return 1; }
            /// {@link Hidden}
            public int32 CannotSeePrivate() { return 1; }
            /// {@link Missing} {@link calculate} {@link return()} {@link @ordinaryName()}
            public int32 MissingTarget() { return 1; }
            /// {@param own}
            public int32 Own(int32 own) { return own; }
            /// {@param other}
            public int32 UnrelatedParameter(int32 own) { return own; }
            /// {@link @return()} and {@link @int32() | escaped built-in spelling}
            public int32 @return() { return 1; }
            public int32 @int32() { return 1; }
            """;
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///DocumentationReferences.sus"), 1, source);
        IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync(
            [snapshot], null, CancellationToken.None);
        SourceAnalysisResult analysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>().Single();
        IReadOnlyList<SushiDiagnostic> diagnostics = results.Single().Reporter.ReportDiagnostics();

        analysis.SemanticOccurrences.Where(item => !item.IsDeclaration)
            .Where(item => item.Span.Snapshot == snapshot)
            .Should().Contain(item => item.Symbol.Name == "Calculate" && item.Symbol.CompilerSymbol is not null)
            .And.Contain(item => item.Symbol.Name == "own" && item.Symbol.Kind is SourceSemanticSymbolKind.Parameter)
            .And.Contain(item => item.Symbol.Name == "return")
            .And.Contain(item => item.Symbol.Name == "int32");

        int declarationOffset = source.IndexOf("Calculate(int32 amount)", StringComparison.Ordinal);
        SourceHoverResult? hover = SourceHoverResolver.Resolve(analysis, declarationOffset, CancellationToken.None);
        hover.Should().NotBeNull();
        hover!.Markdown.Should().Contain("Calculate(int32)")
            .And.Contain("calculation")
            .And.NotContain("{@link");

        int linkTargetOffset = source.IndexOf("Calculate", source.IndexOf("{@link Calculate(int32)", StringComparison.Ordinal), StringComparison.Ordinal);
        analysis.SemanticOccurrences.Should().Contain(item => !item.IsDeclaration
            && item.Span.Start == linkTargetOffset && item.Symbol.Name == "Calculate");
        int escapedBuiltInOffset = source.IndexOf("@int32()", source.IndexOf("{@link @return", StringComparison.Ordinal), StringComparison.Ordinal);
        analysis.SemanticOccurrences.Should().Contain(item => !item.IsDeclaration
            && item.Span.Start == escapedBuiltInOffset && item.Symbol.Name == "int32");

        diagnostics.Select(item => item.DisplayName)
            .Should().Contain("SUSW0013")
            .And.Contain("SUSW0014")
            .And.Contain("SUSW0012")
            .And.Contain("SUSW0015");

        analysis.SemanticOccurrences
            .Where(item => item.Symbol.Name == "Calculate" && !item.IsDeclaration)
            .Should().Contain(item => item.Span.Snapshot.Text.AsSpan(item.Span.Start, item.Span.Length).StartsWith("Calculate", StringComparison.Ordinal));
    }

    [Test]
    public async Task QualifiedDocumentationLinkShouldResolveAcrossProjectSourceFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Documentation " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, "Documentation.susproj");
        string libraryPath = Path.Combine(directory, "Math.sus");
        string callerPath = Path.Combine(directory, "Caller.sus");
        await File.WriteAllTextAsync(projectPath, """
            name = "Documentation"
            assembly = "Documentation"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """);
        await File.WriteAllTextAsync(libraryPath, """
            package Example;
            namespace Math;
            public int32 Calculate(int32 value) { return value; }
            """);
        string caller = """
            package Example;
            namespace Caller;
            /// {@link Example.Math.Calculate(int32) | calculation}
            public int32 Use() { return 1; }
            """;
        await File.WriteAllTextAsync(callerPath, caller);

        try
        {
            SourceSnapshot project = SourceSnapshot.FromText(new Uri(projectPath), 1, await File.ReadAllTextAsync(projectPath));
            SourceSnapshot library = SourceSnapshot.FromText(new Uri(libraryPath), 1, await File.ReadAllTextAsync(libraryPath));
            SourceSnapshot calling = SourceSnapshot.FromText(new Uri(callerPath), 1, caller);
            IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync(
                [project, library, calling], null, CancellationToken.None);
            SourceAnalysisResult callerAnalysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>()
                .Single(item => item.Snapshot.Uri.IsSamePath(new Uri(callerPath)));
            callerAnalysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Symbol.Name == "Calculate")
                .Should().ContainSingle(item => item.Symbol.DefinitionSpan?.Snapshot.Uri.IsSamePath(new Uri(libraryPath)) is true);
            results.SelectMany(item => item.Reporter.ReportDiagnostics())
                .Where(item => item.DisplayName is "SUSW0012" or "SUSW0013" or "SUSW0014" or "SUSW0015")
                .Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}