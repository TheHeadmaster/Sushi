using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Backends.Native;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies graceful and invalid program termination through native executable generation.
/// </summary>
[TestFixture]
public class ProgramTerminationCompilationTests
{
    [TestCase(TestName = "Program Exit Should Terminate From A Nested Function With Its Evaluated Status")]
    public async Task CompileAsyncShould_ExitProgramFromNestedFunction()
    {
        const string source = """
            package Example;
            namespace Example;

            public int32 CalculateStatus()
            {
                return 40 + 2;
            }

            public int32 Terminate()
            {
                int32 status = CalculateStatus();
                exit status;
            }

            public root activity Application : task
            {
                public entry void Start()
                {
                    int32 ignored = Terminate();
                    exit 7;
                }

                public stop void Stop()
                {
                    exit 43;
                }
            }
            """;

        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Diagnostics
                .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
                .Should()
                .BeEmpty();

            compilation.Succeeded.Should().BeTrue();
            await RunAndAssertExitCode(compilation, 42);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestCase(TestName = "Bare Program Exit Should Use Status Zero And Omit Following Instructions")]
    public async Task CompileAsyncShould_DefaultExitToZero()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    exit;
                    exit 12;
                }
            }
            """;

        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Succeeded.Should().BeTrue();
            await RunAndAssertExitCode(compilation, 0);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestCase(TestName = "Conditional And Loop Exits Should Terminate Only When Their Paths Execute")]
    public async Task CompileAsyncShould_ExitFromSelectedConditionalAndLoopPaths()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    if (false)
                    {
                        exit 42;
                    }

                    while (true)
                    {
                        exit 8;
                    }

                    exit 9;
                }
            }
            """;

        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Succeeded.Should().BeTrue();
            await RunAndAssertExitCode(compilation, 8);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestCase(TestName = "Dynamic Reserved Program Exit Status Should Use Fatal Termination")]
    public async Task CompileAsyncShould_FatallyTerminateForDynamicReservedStatus()
    {
        const string source = """
            package Example;
            namespace Example;

            public int32 CalculateStatus(int32 status)
            {
                return status;
            }

            public root activity Application : task
            {
                public entry void Start()
                {
                    exit CalculateStatus(1);
                }
            }
            """;

        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Succeeded.Should().BeTrue();
            await RunAndAssertExitCode(compilation, 1);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestCase("exit 1;", TestName = "Direct Reserved Program Exit Status Should Be Diagnosed")]
    [TestCase("exit 3 - 2;", TestName = "Constant Expression Producing Reserved Program Exit Status Should Be Diagnosed")]
    public async Task CompileAsyncShould_RejectConstantReservedStatus(string invalidExit)
    {
        string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    exit;
                    __INVALID_EXIT__
                }
            }
            """.Replace("__INVALID_EXIT__", invalidExit, StringComparison.Ordinal);

        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Succeeded.Should().BeFalse();
            compilation.Executable.Should().BeNull();
            compilation.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.IsType(ErrorType.InvalidApplicationExitStatus));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestCase("exit 0;", 0, TestName = "Explicit Zero Exit Should Complete Successfully")]
    [TestCase("exit 2 + 3;", 5, TestName = "Computed Exit Status Should Be Preserved")]
    public async Task CompileAsyncShould_PreserveExplicitStatuses(string exitStatement, int expectedStatus)
    {
        string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    __EXIT_STATEMENT__
                }
            }
            """.Replace("__EXIT_STATEMENT__", exitStatement, StringComparison.Ordinal);
        (string directory, MVPCompilationResult compilation) = await CompileProjectAsync(source);

        try
        {
            compilation.Succeeded.Should().BeTrue();
            await RunAndAssertExitCode(compilation, expectedStatus);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static async Task<(string Directory, MVPCompilationResult Compilation)> CompileProjectAsync(string source)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "ProgramTermination", Guid.NewGuid().ToString("N"));
        string buildDirectory = Path.Combine(directory, "Build");
        Directory.CreateDirectory(buildDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(directory, ".susproj"),
            """
            name = "Program Termination"
            assembly = "ProgramTermination"
            language-version = "1.0"

            [build]
            default = "debug"
            sources = ["Build/**/*.sus"]

            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """,
            CancellationToken.None);

        await File.WriteAllTextAsync(Path.Combine(buildDirectory, "Program.sus"), source, CancellationToken.None);
        MVPCompilationResult compilation = await new MVPProjectCompiler().CompileAsync(Path.Combine(directory, ".susproj"), CancellationToken.None);
        return (directory, compilation);
    }

    private static async Task RunAndAssertExitCode(MVPCompilationResult compilation, int expectedExitCode)
    {
        NativeExecutableArtifact executable = compilation.Executable!;
        executable.Should().NotBeNull();

        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable.FilePath,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start().Should().BeTrue();
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(expectedExitCode);
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
