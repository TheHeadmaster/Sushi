using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies source diagnostics use the compiler's project-level callable resolution.
/// </summary>
[TestFixture]
public class ProjectDiagnosticAnalyzerTests
{
    private const string Header = "package Example; namespace Example;\n";

    [TestCase(TestName = "Standalone Source Binds Same File Void And Value Returning Calls")]
    public async Task StandaloneSourceShouldResolveItsOwnPackageFunctions()
    {
        string source = Header + "public void Notify() { return; }\n"
            + "public int32 Twice(int32 value) { return value * 2; }\n"
            + "public bool Enabled(bool value) { return value; }\n"
            + "public int32 Caller(int32 input) { Notify(); int32 doubled = Twice(input); if (Enabled(value: true)) { doubled = doubled + 1; } return doubled + Twice(value: 1); }";
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///Standalone.sus"), 1, source);

        IReadOnlyList<ProjectDiagnosticResult> results = await new ProjectDiagnosticAnalyzer().AnalyzeAsync([snapshot], null, CancellationToken.None);

        ProjectDiagnosticResult result = results
            .Should()
            .ContainSingle()
            .Which;
        result.Reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
        ((Sushi.Analysis.SourceAnalysisResult)result.Analysis).Functions
            .Should()
            .HaveCount(4);
    }

    [TestCase(TestName = "Project Analysis Binds The InspectMe Cross File Reproduction Without A Root Activity")]
    public async Task ProjectFunctionsShouldResolveAcrossSourceFiles()
    {
        using DiagnosticProject project = await DiagnosticProject.Create();
        string caller = Header + "public int32 InspectMe(int32 input)\n{\n"
            + "    int32 doubled = InspectMe2(input);\n\n"
            + "    if (doubled > 10)\n    {\n        return doubled;\n    }\n\n"
            + "    return doubled + 1;\n}";
        string callee = Header + "public int32 InspectMe2(int32 input) { return input * 2; }";
        IReadOnlyList<ProjectDiagnosticResult> results = await project.Analyze(caller, callee);
        ProjectDiagnosticResult callerResult = results.Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri));

        callerResult.Reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
        ((Sushi.Analysis.SourceAnalysisResult)callerResult.Analysis).Functions
            .Should()
            .ContainSingle();
        Directory.Exists(Path.Combine(project.DirectoryPath, "bin"))
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Project Analysis Binds Cross File Void Calls")]
    public async Task ProjectVoidFunctionsShouldResolveAcrossSourceFiles()
    {
        using DiagnosticProject project = await DiagnosticProject.Create();
        string caller = Header + "public void Caller() { Notify(); return; }";
        string callee = Header + "public void Notify() { return; }";
        IReadOnlyList<ProjectDiagnosticResult> results = await project.Analyze(caller, callee);

        results.Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri)).Reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Unsaved Dependency Signature Changes Update And Then Clear Caller Diagnostics")]
    public async Task CurrentDependencySnapshotShouldControlCallableBinding()
    {
        using DiagnosticProject project = await DiagnosticProject.Create();
        string caller = Header + "public int32 Caller(int32 input) { return Helper(input); }";
        string validCallee = Header + "public int32 Helper(int32 value) { return value + 1; }";
        string invalidCallee = Header + "public int32 Helper(bool value) { return 1; }";

        IReadOnlyList<ProjectDiagnosticResult> valid = await project.Analyze(caller, validCallee, callerVersion: 10, calleeVersion: 3);
        IReadOnlyList<ProjectDiagnosticResult> invalid = await project.Analyze(caller, invalidCallee, callerVersion: 10, calleeVersion: 4);
        IReadOnlyList<ProjectDiagnosticResult> restored = await project.Analyze(caller, validCallee, callerVersion: 10, calleeVersion: 5);

        valid.Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri)).Reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
        invalid.Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri)).Reporter.ReportDiagnostics()
            .Should()
            .NotBeEmpty();
        restored.Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri)).Reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invalid Project Calls Remain Diagnosed Instead Of Being Silently Accepted")]
    public async Task UnknownFunctionsAndDiscardedValuesShouldRemainErrors()
    {
        using DiagnosticProject project = await DiagnosticProject.Create();
        string caller = Header + "public int32 Value(int32 input) { return input; }\n"
            + "private int32 Hidden(int32 input) { return input; }\n"
            + "public void Nothing() { return; }\n"
            + "public int32 Number(int32 input) { return input; }\n"
            + "public int32 Value() { return 2; }\n"
            + "public int32 Caller() { Value(1); Hidden(1); Value(true); Number(true); Number(); Number(input: 1, input: 2); Missing(1); Nothing(); return Nothing(); }";
        IReadOnlyList<ProjectDiagnosticResult> results = await project.Analyze(caller, Header);
        IReadOnlyList<SushiDiagnostic> diagnostics = results
            .Single(result => result.Analysis.Snapshot.Uri.IsSamePath(project.CallerUri))
            .Reporter.ReportDiagnostics();

        diagnostics
            .Should()
            .HaveCountGreaterThan(5)
            .And
            .OnlyContain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    private sealed class DiagnosticProject : IDisposable
    {
        private const string Configuration = """
            name = "Diagnostics"
            assembly = "Diagnostics"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;

        public string DirectoryPath { get; }
        public Uri ProjectUri { get; }
        public Uri CallerUri { get; }
        public Uri CalleeUri { get; }

        private DiagnosticProject(string directoryPath)
        {
            this.DirectoryPath = directoryPath;
            this.ProjectUri = new Uri(Path.Combine(directoryPath, "Diagnostics.susproj"));
            this.CallerUri = new Uri(Path.Combine(directoryPath, "Sources", "A.sus"));
            this.CalleeUri = new Uri(Path.Combine(directoryPath, "Sources", "B.sus"));
        }

        public static async Task<DiagnosticProject> Create()
        {
            string directoryPath = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Project Diagnostics " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directoryPath, "Sources"));
            DiagnosticProject project = new(directoryPath);
            await File.WriteAllTextAsync(project.ProjectUri.LocalPath, Configuration);
            await File.WriteAllTextAsync(project.CallerUri.LocalPath, Header);
            await File.WriteAllTextAsync(project.CalleeUri.LocalPath, Header);
            return project;
        }

        public async Task<IReadOnlyList<ProjectDiagnosticResult>> Analyze(
            string caller,
            string callee,
            int callerVersion = 1,
            int calleeVersion = 1)
        {
            SourceSnapshot project = SourceSnapshot.FromText(this.ProjectUri, null, await File.ReadAllTextAsync(this.ProjectUri.LocalPath));
            SourceSnapshot callerSnapshot = SourceSnapshot.FromText(this.CallerUri, callerVersion, caller);
            SourceSnapshot calleeSnapshot = SourceSnapshot.FromText(this.CalleeUri, calleeVersion, callee);
            return await new ProjectDiagnosticAnalyzer().AnalyzeAsync([project, callerSnapshot, calleeSnapshot], null, CancellationToken.None);
        }

        public void Dispose()
        {
            Directory.Delete(this.DirectoryPath, recursive: true);
        }
    }
}
