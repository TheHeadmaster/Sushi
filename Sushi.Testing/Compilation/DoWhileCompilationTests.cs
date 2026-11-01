using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies post-test loop semantics, mixed nesting, loop transfers, syntax
/// diagnostics, and native execution through the complete Sushi pipeline.
/// </summary>
[TestFixture]
public class DoWhileCompilationTests
{
    private const string Helpers = """
        package Example;
        namespace Example;

        public int32 Divide(int32 divisor)
        {
            return 10 / divisor;
        }

        public bool IsPositive(int32 value)
        {
            return value > 0;
        }

        public void Fail()
        {
            int32 result = Divide(0);
            return;
        }

        public void Ensure(int32 actual, int32 expected)
        {
            if (actual != expected)
            {
                Fail();
            }
        }

        public void Require(bool condition)
        {
            if (not condition)
            {
                Fail();
            }
        }
        """;

    [TestCase("int32 count = 0; do { count += 1; } while (false) Ensure(count, 1);", TestName = "Do While Should Execute Body Once When Condition Is False")]
    [TestCase("int32 count = 0; do { count += 1; } while (count < 5) Ensure(count, 5);", TestName = "Do While Should Repeat Until Condition Becomes False")]
    [TestCase("int32 count = 10; do { count += 1; } while (count < 5) Ensure(count, 11);", TestName = "Do While Should Test Condition After First Iteration")]
    [TestCase("int32 count = 1; do { count *= 2; } while (count < 10) Ensure(count, 16);", TestName = "Do While Should Support Compound Assignment")]
    [TestCase("bool running = true; int32 count = 0; do { count += 1; running = count < 3; } while (running) Ensure(count, 3);", TestName = "Boolean Local Should Control Post Test Loop")]
    [TestCase("do { } while (false)", TestName = "Empty Do While Body Should Be Valid")]
    [TestCase("do { break; } while (true)", TestName = "Break Should Terminate Constant True Do While")]
    [TestCase("int32 count = 0; do { count += 1; break; } while (true) Ensure(count, 1);", TestName = "Break Should Skip Further Iterations")]
    [TestCase("bool flag = false; do { flag = true; } while (not flag) Require(flag);", TestName = "Post Test Condition Should Observe Body Mutation")]
    [TestCase("bool flag = false; do { flag = true; } while (flag and false) Require(flag);", TestName = "Post Test Condition Should Preserve Logical Operators")]
    public async Task DoWhileShouldExecute(string body)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Diagnostics
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

    [TestCase(TestName = "Break Should Bypass Trailing Condition")]
    public async Task BreakShouldNotEvaluateTrailingCondition()
    {
        string body = """
            int32 divisor = 1;

            do
            {
                divisor = 0;
                break;
            } while (IsPositive(Divide(divisor)))

            Ensure(divisor, 0);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Continue Should Reach Trailing Condition Rather Than Restart Body")]
    public async Task ContinueShouldEvaluateTrailingCondition()
    {
        string body = """
            int32 count = 0;

            do
            {
                count += 1;

                if (count < 3)
                {
                    continue;
                }

                if (count > 3)
                {
                    Fail();
                }
            } while (count < 3)

            Ensure(count, 3);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Repeated Break Should Exit Outer Do While From Inner While")]
    public async Task RepeatedBreakShouldExitOuterDoWhile()
    {
        string body = """
            int32 count = 0;

            do
            {
                count += 1;

                while (true)
                {
                    break break;
                }

                Fail();
            } while (IsPositive(Divide(0)))

            Ensure(count, 1);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Repeated Continue Should Reach Outer Do While Condition")]
    public async Task RepeatedContinueShouldTargetOuterDoWhile()
    {
        string body = """
            int32 count = 0;

            do
            {
                count += 1;

                while (true)
                {
                    continue continue;
                }

                Fail();
            } while (count < 3)

            Ensure(count, 3);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Repeated Continue From Do While Should Target Outer While Condition")]
    public async Task RepeatedContinueShouldTargetOuterWhile()
    {
        string body = """
            int32 count = 0;

            while (count < 3)
            {
                do
                {
                    count += 1;
                    continue continue;
                } while (IsPositive(Divide(0)))

                Fail();
            }

            Ensure(count, 3);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Nested Post Test Loops Should Preserve Independent Storage")]
    public async Task NestedDoWhileLoopsShouldExecute()
    {
        string body = """
            int32 outer = 0;
            int32 total = 0;

            do
            {
                outer += 1;
                int32 inner = 0;

                do
                {
                    inner += 1;
                    total += 1;
                } while (inner < 2)
            } while (outer < 3)

            Ensure(outer, 3);
            Ensure(total, 6);
            """;

        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Return Inside Do While Should Exit The Enclosing Function")]
    public async Task ReturnInsideDoWhileShouldExitFunction()
    {
        string functions = Helpers + """

            public int32 Find(int32 divisor)
            {
                int32 candidate = 1;

                do
                {
                    if (candidate % divisor == 0)
                    {
                        return candidate;
                    }

                    candidate += 1;
                } while (candidate < 10)

                return 0;
            }

            public int32 SinglePass(bool flag)
            {
                int32 result = 0;

                do
                {
                    result = 5;

                    if (flag)
                    {
                        return result;
                    }
                } while (false)

                return result + 1;
            }
            """;

        string root = CreateRoot("""
            Ensure(Find(3), 3);
            Ensure(Find(5), 5);
            Ensure(SinglePass(true), 5);
            Ensure(SinglePass(false), 6);
            """);

        using TestProject project = await TestProject.Create(root, functions);
        MVPCompilationResult result = await project.Compile();

        result.Diagnostics
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

    [TestCase("do { } while (1)", ErrorType.UnsupportedMVPExpression, TestName = "Post Test Condition Must Be Boolean")]
    [TestCase("do { Missing(); } while (false)", ErrorType.UnsupportedMVPExpression, TestName = "Post Test Body Must Be Checked Even When Condition Is False")]
    [TestCase("do { break break; } while (false)", ErrorType.UnsupportedMVPExpression, TestName = "Excessive Break Depth Must Be Rejected")]
    [TestCase("do { continue continue; } while (false)", ErrorType.UnsupportedMVPExpression, TestName = "Excessive Continue Depth Must Be Rejected")]
    [TestCase("do { int32 inner = 1; } while (false) inner = 2;", ErrorType.UnknownIdentifier, TestName = "Post Test Body Local Must Not Escape Scope")]
    [TestCase("do { int32 inner = 1; } while (inner < 2)", ErrorType.UnknownIdentifier, TestName = "Post Test Condition Must Not See Body Local")]
    public async Task InvalidDoWhileShouldProduceDiagnostic(string body, ErrorType expectedError)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Executable
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(expectedError));
    }

    [TestCase("do { } while (false);", TestName = "Do While Must Not Accept Trailing Semicolon")]
    [TestCase("do { } while false", TestName = "Post Test Condition Requires Parentheses")]
    [TestCase("do { }", TestName = "Do While Requires Trailing While Clause")]
    [TestCase("do { } while ()", TestName = "Do While Requires Condition Expression")]
    [TestCase("do { break } while (false)", TestName = "Break Still Requires Semicolon")]
    [TestCase("do { continue } while (false)", TestName = "Continue Still Requires Semicolon")]
    public async Task MalformedDoWhileShouldProduceSyntaxDiagnostic(string body)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Executable
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Potentially Completing Post Test Loop Must Not Satisfy Value Return Contract Alone")]
    public async Task PostTestLoopShouldRequireFollowingReturnWhenFallthroughIsPossible()
    {
        string functions = Helpers + """

            public int32 Invalid(bool flag)
            {
                do
                {
                    if (flag)
                    {
                        return 1;
                    }
                } while (false)
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), functions);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.MissingReturnPath));
    }

    private static async Task AssertNativeSuccess(string body)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Diagnostics
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

    private static string CreateRoot(string body)
        => $$"""
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    {{body}}
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

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            throw new TimeoutException("The compiled Sushi executable did not terminate within the test timeout.");
        }

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
                    name = "Do While Loops"
                    assembly = "DoWhileLoops"
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
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

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