using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies that parsed invocation syntax is explicitly rejected until executable call binding is implemented.
/// </summary>
[TestFixture]
public class InvocationCompilationBoundaryTests
{
    private static readonly Uri testUri = new("file:///TestProject/InvocationBoundary.sus");

    [TestCase("Helper();", TestName = "Function Binder Should Diagnose Unsupported Invocation Statement")]
    [TestCase("Helper(42);", TestName = "Function Binder Should Diagnose Unsupported Invocation With Arguments")]
    [TestCase("Helper() + 1;", TestName = "Function Binder Should Diagnose Unsupported Expression Statement")]
    [TestCase("return; Helper();", TestName = "Function Binder Should Diagnose Unreachable Invocation")]
    public async Task FunctionShould_0(string body)
    {
        string source = $$"""
            package Example;
            namespace Example;

            public void Launch()
            {
                {{body}}
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(
            snapshot, diagnosticReporter, CancellationToken.None);

        result
            .Should()
            .NotBeNull();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Function Binder Should Diagnose Unsupported Invocation In Return Expression")]
    public async Task FunctionShould_1()
    {
        const string source = """
            package Example;
            namespace Example;

            public int32 Calculate()
            {
                return Compute();
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        await new SourceAnalyzer().Analyze(snapshot, diagnosticReporter, CancellationToken.None);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Executable Root Should Diagnose Unsupported Invocation")]
    public async Task RootShould_0()
    {
        string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    Initialize();
                }
            }
            """;

        await AssertRootInvocationDiagnostic(source);
    }

    [TestCase(TestName = "Executable Root Should Diagnose Unreachable Invocation")]
    public async Task RootShould_1()
    {
        string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    return;

                    if (true)
                    {
                        Initialize();
                    }
                }
            }
            """;

        await AssertRootInvocationDiagnostic(source);
    }

    private static async Task AssertRootInvocationDiagnostic(string source)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string buildDirectory = Path.Combine(directory, "Build");

        Directory.CreateDirectory(buildDirectory);

        try
        {
            string projectPath = Path.Combine(directory, ".susproj");

            await File.WriteAllTextAsync(
                projectPath,
                """
                name = "Invocation Boundary"
                assembly = "InvocationBoundary"
                language-version = "1.0"

                [build]
                default = "debug"
                sources = ["Build/**/*.sus"]

                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """);

            await File.WriteAllTextAsync(Path.Combine(buildDirectory, "Program.sus"), source);

            MVPCompilationResult result = await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);

            result.Succeeded
                .Should()
                .BeFalse();

            result.Executable
                .Should()
                .BeNull();

            result.Diagnostics
                .Should()
                .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}