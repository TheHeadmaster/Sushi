using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies direct package-level void calls from task-root lifecycle methods
/// through the full native executable pipeline.
/// </summary>
[TestFixture]
public class DirectPackageCallCompilationTests
{
    private const string GuardFunction = """
        package Example;
        namespace Example;

        public void Guard(int32 divisor, bool enabled)
        {
            if (enabled and (10 / divisor > 0))
            {
                return;
            }
        }
        """;

    [TestCase("Guard(enabled: false, divisor: 0);", TestName = "Named Arguments Should Preserve Short Circuit Evaluation")]
    [TestCase("Guard(divisor: 2, enabled: true);", TestName = "Named Arguments Should Bind In Declaration Order")]
    [TestCase("Guard(2, true);", TestName = "Positional Arguments Should Invoke Package Function")]
    [TestCase("Guard(2, enabled: true);", TestName = "Mixed Arguments Should Invoke Package Function")]
    public async Task DirectCallShould_0(string invocation)
    {
        string root = CreateRoot(invocation);

        using TestProject project = await TestProject.Create(root, GuardFunction);
        MVPCompilationResult result = await project.Compile();

        result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == Sushi.Diagnostics.DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        result.Succeeded
            .Should()
            .BeTrue();

        int exitCode = await RunExecutable(result.Executable!.FilePath);

        exitCode
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Direct Package Call Should Execute Runtime Arithmetic Failure")]
    public async Task DirectCallShould_1()
    {
        using TestProject project = await TestProject.Create(
            CreateRoot("Guard(enabled: true, divisor: 0);"),
            GuardFunction);

        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeTrue();

        int exitCode = await RunExecutable(result.Executable!.FilePath);

        exitCode
            .Should()
            .NotBe(0);
    }

    [TestCase(TestName = "Stop Lifecycle Should Invoke Package Function")]
    public async Task DirectCallShould_2()
    {
        string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    return;
                }

                public stop void Cleanup()
                {
                    Guard(enabled: true, divisor: 0);
                }
            }
            """;

        using TestProject project = await TestProject.Create(source, GuardFunction);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeTrue();

        int exitCode = await RunExecutable(result.Executable!.FilePath);

        exitCode
            .Should()
            .NotBe(0);
    }

    [TestCase("Guard(enabled: true, 2);", TestName = "Positional Argument After Named Argument Should Be Rejected")]
    [TestCase("Guard(divisor: 2, divisor: 4, enabled: true);", TestName = "Duplicate Named Argument Should Be Rejected")]
    [TestCase("Guard(divisor: 2);", TestName = "Missing Required Argument Should Be Rejected")]
    [TestCase("Guard(2, enabled: true, extra: 1);", TestName = "Unknown Named Argument Should Be Rejected")]
    [TestCase("Guard(divisor: true, enabled: false);", TestName = "Mismatched Argument Type Should Be Rejected")]
    [TestCase("Missing();", TestName = "Unknown Package Function Should Be Rejected")]
    public async Task InvalidCallShould_0(string invocation)
    {
        using TestProject project = await TestProject.Create(CreateRoot(invocation), GuardFunction);
        MVPCompilationResult result = await project.Compile();

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

    [TestCase(TestName = "Value Returning Package Function Cannot Be Used As Standalone Statement")]
    public async Task InvalidCallShould_1()
    {
        const string helper = """
            package Example;
            namespace Example;

            public int32 Calculate()
            {
                return 42;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Calculate();"), helper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Overloaded Function Name Should Not Be Arbitrarily Selected")]
    public async Task InvalidCallShould_2()
    {
        const string helper = """
            package Example;
            namespace Example;

            public void Configure(int32 value)
            {
                return;
            }

            public void Configure(bool value)
            {
                return;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Configure(42);"), helper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    private static string CreateRoot(string invocation)
        => $$"""
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    {{invocation}}
                }
            }
            """;

    private static async Task<int> RunExecutable(string executablePath)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start()
            .Should()
            .BeTrue();

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private sealed class TestProject(string directory) : IDisposable
    {
        private readonly string directory = directory;

        public static async Task<TestProject> Create(string rootSource, string helperSource)
        {
            string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
            string buildDirectory = Path.Combine(directory, "Build");

            Directory.CreateDirectory(buildDirectory);

            try
            {
                await File.WriteAllTextAsync(
                    Path.Combine(directory, ".susproj"),
                    """
                    name = "Direct Package Call"
                    assembly = "DirectPackageCall"
                    language-version = "1.0"

                    [build]
                    default = "debug"
                    sources = ["Build/**/*.sus"]

                    [build.targets.debug]
                    type = "Sushi.MVP.Debug"
                    """);

                await File.WriteAllTextAsync(Path.Combine(buildDirectory, "Program.sus"), rootSource);
                await File.WriteAllTextAsync(Path.Combine(buildDirectory, "Helpers.sus"), helperSource);
                return new TestProject(directory);
            }
            catch
            {
                Directory.Delete(directory, recursive: true);
                throw;
            }
        }

        public Task<MVPCompilationResult> Compile()
            => new MVPProjectCompiler().CompileAsync(Path.Combine(this.directory, ".susproj"), CancellationToken.None);

        public void Dispose()
        {
            if (Directory.Exists(this.directory))
            {
                Directory.Delete(this.directory, recursive: true);
            }
        }
    }
}