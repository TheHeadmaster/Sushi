
using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies project compilation and native execution of the supported synchronous task-root lifecycle.
/// </summary>
[TestFixture]
public class RootActivityCompilationTests
{
    [TestCase(true, TestName = "Task Root With Stop Should Execute Successfully")]
    [TestCase(false, TestName = "Task Root Without Stop Should Execute Successfully")]
    public async Task TaskRootShould_0(bool includeStop)
    {
        string stopMethod = includeStop ? "public stop void Cleanup() { return; }" : string.Empty;

        string source = $$"""
            package Example;
            namespace Example;

            public int32 Helper() { return 42; }

            public root activity Application : task
            {
                public entry void Launch() { return; }
                {{stopMethod}}
            }
            """;

        await AssertRunnableRoot(source);
    }

    [TestCase("if (true) { return; }", TestName = "Task Root Should Execute Conditional Early Return")]
    [TestCase("if (false) { return; }", TestName = "Task Root Should Fall Through False Conditional")]
    [TestCase("if (false) { return; } else { return; }", TestName = "Task Root Should Execute Else Branch")]
    [TestCase("if (false) { return; } else if (true) { return; } else { return; }", TestName = "Task Root Should Execute Else If Chain")]
    [TestCase("if (true and not false) { return; }", TestName = "Task Root Should Execute Boolean Conditional")]
    [TestCase("return; return;", TestName = "Task Root Should Accept Multiple Returns")]
    [TestCase("return; if (true) { return; }", TestName = "Task Root Should Accept Valid Unreachable Statements")]
    public async Task ConditionalRootShould_0(string entryBody)
    {
        string source = $$"""
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Launch()
                {
                    {{entryBody}}
                }

                public stop void Cleanup()
                {
                    if ((6 * 7) == 42)
                    {
                        return;
                    }
                }
            }
            """;

        await AssertRunnableRoot(source);
    }

    [TestCase(TestName = "Executable Project Should Reject Missing Root Activity")]
    public async Task MissingRootShould_0()
    {
        await AssertCompilationError(
            "public void Launch() { return; }",
            ErrorType.MissingRootActivity);
    }

    [TestCase(TestName = "Executable Project Should Reject Duplicate Roots")]
    public async Task DuplicateRootShould_0()
    {
        await AssertCompilationError(
            """
            public root activity First : task
            {
                public entry void Start() {}
            }

            public root activity Second : task
            {
                public entry void Start() {}
            }
            """,
            ErrorType.DuplicateRootActivity);
    }

    [TestCase(TestName = "Native Bootstrap Should Reject Persistent Root")]
    public async Task PersistentRootShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : persistent
            {
                public entry void Start() {}
            }
            """,
            ErrorType.UnsupportedRootActivityLifetime);
    }

    [TestCase(TestName = "Native Bootstrap Should Reject Stop Context Parameters")]
    public async Task StopParametersShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start() {}
                public stop void Cleanup(int32 context) {}
            }
            """,
            ErrorType.UnsupportedRootLifecycleParameters);
    }

    [TestCase(TestName = "Root Entry Should Reject Value Bearing Return")]
    public async Task InvalidEntryReturnShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start() { return 42; }
            }
            """,
            ErrorType.ReturnValueInVoidFunction);
    }

    [TestCase(TestName = "Root Stop Should Reject Value Bearing Return")]
    public async Task InvalidStopReturnShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start() {}
                public stop void Cleanup() { return 42; }
            }
            """,
            ErrorType.ReturnValueInVoidFunction);
    }

    [TestCase(TestName = "Root Entry Should Reject Non Boolean Condition")]
    public async Task InvalidConditionShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start()
                {
                    if (42)
                    {
                        return;
                    }
                }
            }
            """,
            ErrorType.UnsupportedMVPExpression);
    }

    [TestCase(TestName = "Root Entry Should Check Unreachable Conditional Branches")]
    public async Task InvalidUnreachableBranchShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start()
                {
                    if (true)
                    {
                        return;
                    }
                    else
                    {
                        return 42;
                    }
                }
            }
            """,
            ErrorType.ReturnValueInVoidFunction);
    }

    [TestCase(TestName = "Root Entry Should Check Statements After Unconditional Return")]
    public async Task InvalidUnreachableStatementShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start()
                {
                    return;

                    if (42)
                    {
                        return;
                    }
                }
            }
            """,
            ErrorType.UnsupportedMVPExpression);
    }

    [TestCase(TestName = "Root Entry Should Reject Unbraced Conditional Branch")]
    public async Task InvalidUnbracedBranchShould_0()
    {
        await AssertCompilationError(
            """
            public root activity Application : task
            {
                public entry void Start()
                {
                    if (true)
                        return;
                }
            }
            """,
            ErrorType.ExpectedSyntax);
    }

    private static async Task AssertRunnableRoot(string source)
    {
        string directory = CreateTestDirectory();

        try
        {
            MVPCompilationResult result = await CompileAsync(directory, source);

            result.Diagnostics
                .Should()
                .BeEmpty();

            result.Succeeded
                .Should()
                .BeTrue();

            string executablePath = result.Executable!.FilePath;

            File.Exists(executablePath)
                .Should()
                .BeTrue();

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

            process.ExitCode
                .Should()
                .Be(0);
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static async Task AssertCompilationError(string declarations, ErrorType expected)
    {
        string directory = CreateTestDirectory();

        try
        {
            string source = $"package Example;\nnamespace Example;\n\n{declarations}";
            MVPCompilationResult result = await CompileAsync(directory, source);

            result.Succeeded
                .Should()
                .BeFalse();

            result.Executable
                .Should()
                .BeNull();

            result.Diagnostics
                .Should()
                .Contain(diagnostic => diagnostic.IsType(expected));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static async Task<MVPCompilationResult> CompileAsync(string directory, string source)
    {
        Directory.CreateDirectory(Path.Combine(directory, "Build"));

        string projectPath = Path.Combine(directory, ".susproj");

        await File.WriteAllTextAsync(
            projectPath,
            """
            name = "Root Activity Test"
            assembly = "RootActivityTest"
            language-version = "1.0"

            [build]
            default = "debug"
            sources = ["Build/**/*.sus"]

            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """);

        await File.WriteAllTextAsync(Path.Combine(directory, "Build", "Program.sus"), source);

        return await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);
    }

    private static string CreateTestDirectory()
        => Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));

    private static void DeleteTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
