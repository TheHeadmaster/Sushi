using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
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
            /// A matched multiline ` span starts here.
            /// {@link Missing} is illustrative code.
            /// and closes here ` before the next reference.
            /// An unmatched ` delimiter precedes {@link Calculate}.
            public int32 Calculate() { return 1; }
            /// {@link Convert(int32)} and {@link Example.Example.Convert(int32) | calculation}.
            public int32 Convert(int32 amount) { return amount; }
            public int32 Convert() { return 1; }
            /// {@link Convert}
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

        analysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Span.Snapshot == snapshot)
            .Any(item => item.Symbol.Name == "Calculate" && item.Symbol.CompilerSymbol is not null).Should().BeTrue();
        analysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Span.Snapshot == snapshot)
            .Any(item => item.Symbol.Name == "own" && item.Symbol.Kind == SourceSemanticSymbolKind.Parameter).Should().BeTrue();
        analysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Span.Snapshot == snapshot)
            .Any(item => item.Symbol.Name == "return").Should().BeTrue();
        analysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Span.Snapshot == snapshot)
            .Any(item => item.Symbol.Name == "int32").Should().BeTrue();

        int declarationOffset = source.IndexOf("Convert(int32 amount)", StringComparison.Ordinal);
        SourceHoverResult? hover = SourceHoverResolver.Resolve(analysis, declarationOffset, CancellationToken.None);
        hover.Should().NotBeNull();
        hover!.Markdown.Should().Contain("Convert(int32)")
            .And.Contain("calculation")
            .And.NotContain("{@link");

        int linkTargetOffset = source.IndexOf("Convert", source.IndexOf("{@link Convert(int32)", StringComparison.Ordinal), StringComparison.Ordinal);
        analysis.SemanticOccurrences.Any(item => !item.IsDeclaration
            && item.Span.Start == linkTargetOffset && item.Symbol.Name == "Convert").Should().BeTrue();
        int unmatchedReferenceOffset = source.IndexOf("{@link Calculate}", source.IndexOf("An unmatched", StringComparison.Ordinal), StringComparison.Ordinal) + "{@link ".Length;
        analysis.SemanticOccurrences.Any(item => !item.IsDeclaration
            && item.Span.Start == unmatchedReferenceOffset && item.Symbol.Name == "Calculate").Should().BeTrue();
        int multilineCodeReferenceOffset = source.IndexOf("{@link Missing}", StringComparison.Ordinal);
        analysis.SemanticOccurrences.Any(item => !item.IsDeclaration && item.Span.Start == multilineCodeReferenceOffset).Should().BeFalse();
        int escapedBuiltInOffset = source.IndexOf("@int32()", source.IndexOf("{@link @return", StringComparison.Ordinal), StringComparison.Ordinal);
        analysis.SemanticOccurrences.Any(item => !item.IsDeclaration
            && item.Span.Start == escapedBuiltInOffset && item.Symbol.Name == "int32").Should().BeTrue();

        analysis.SemanticOccurrences.Any(item => !item.IsDeclaration && (item.Symbol.Name == "Missing" || item.Symbol.Name == "Hidden")).Should().BeFalse();

        diagnostics.Select(item => item.DisplayName)
            .Should().Contain("SUSW0013")
            .And.Contain("SUSW0014")
            .And.Contain("SUSW0012")
            .And.Contain("SUSW0015");

        analysis.SemanticOccurrences
            .Where(item => item.Symbol.Name == "Calculate" && !item.IsDeclaration)
            .Any(item => item.Span.Snapshot.Text.Substring(item.Span.Start, item.Span.Length).StartsWith("Calculate", StringComparison.Ordinal)).Should().BeTrue();
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
            package int32 PackageOnly(int32 value) { return value; }
            """);
        string caller = """
            package Caller;
            namespace Caller;
            /// {@link Example.Math.Calculate(int32) | calculation}
            /// {@link Example.Math.PackageOnly(int32)}
            public int32 Use() { return 1; }
            """;
        await File.WriteAllTextAsync(callerPath, caller);
        string samePackage = """
            package Example;
            namespace Client;
            /// {@link Example.Math.PackageOnly(int32)}
            public int32 UsePackageFunction() { return 1; }
            """;
        string samePackagePath = Path.Combine(directory, "SamePackage.sus");
        await File.WriteAllTextAsync(samePackagePath, samePackage);

        try
        {
            SourceSnapshot project = SourceSnapshot.FromText(new Uri(projectPath), 1, await File.ReadAllTextAsync(projectPath));
            SourceSnapshot library = SourceSnapshot.FromText(new Uri(libraryPath), 1, await File.ReadAllTextAsync(libraryPath));
            SourceSnapshot calling = SourceSnapshot.FromText(new Uri(callerPath), 1, caller);
            SourceSnapshot samePackageCalling = SourceSnapshot.FromText(new Uri(samePackagePath), 1, samePackage);
            IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync(
                [project, library, calling, samePackageCalling], null, CancellationToken.None);
            SourceAnalysisResult callerAnalysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>()
                .Single(item => item.Snapshot.Uri.IsSamePath(new Uri(callerPath)));
            callerAnalysis.SemanticOccurrences.Where(item => !item.IsDeclaration && item.Symbol.Name == "Calculate"
                && item.Symbol.DefinitionSpan.HasValue
                && item.Symbol.DefinitionSpan.Value.Snapshot.Uri.IsSamePath(new Uri(libraryPath)))
                .Should().ContainSingle();
            int inaccessibleReferenceOffset = caller.IndexOf("PackageOnly", StringComparison.Ordinal);
            SourceDefinitionResolver.Resolve(callerAnalysis, inaccessibleReferenceOffset, CancellationToken.None).Should().BeNull();
            using (WorkspaceOrchestrator workspace = new())
            {
                await workspace.OpenDocument(new Uri(projectPath), 1, await File.ReadAllTextAsync(projectPath), CancellationToken.None);
                await workspace.OpenDocument(new Uri(libraryPath), 1, await File.ReadAllTextAsync(libraryPath), CancellationToken.None);
                await workspace.OpenDocument(new Uri(callerPath), 1, caller, CancellationToken.None);
                await workspace.OpenDocument(new Uri(samePackagePath), 1, samePackage, CancellationToken.None);
                WorkspaceEdit? rename = await new SourceRenameHandler(workspace).Handle(new RenameParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(callerPath) },
                    Position = new Position(3, caller.Split('\n')[3].IndexOf("PackageOnly", StringComparison.Ordinal)),
                    NewName = "Renamed"
                }, CancellationToken.None);
                rename.Should().BeNull();
            }
            callerAnalysis.SemanticOccurrences.Any(item => !item.IsDeclaration && item.Symbol.Name == "PackageOnly").Should().BeFalse();
            SourceAnalysisResult samePackageAnalysis = results.Select(item => item.Analysis).OfType<SourceAnalysisResult>()
                .Single(item => item.Snapshot.Uri.IsSamePath(new Uri(samePackagePath)));
            samePackageAnalysis.SemanticOccurrences.Any(item => !item.IsDeclaration && item.Symbol.Name == "PackageOnly"
                && item.Symbol.DefinitionSpan.HasValue
                && item.Symbol.DefinitionSpan.Value.Snapshot.Uri.IsSamePath(new Uri(libraryPath))).Should().BeTrue();
            results.SelectMany(item => item.Reporter.ReportDiagnostics())
                .Where(item => item.DisplayName is "SUSW0012" or "SUSW0013" or "SUSW0014" or "SUSW0015")
                .Should().ContainSingle(item => item.DisplayName == "SUSW0014");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}