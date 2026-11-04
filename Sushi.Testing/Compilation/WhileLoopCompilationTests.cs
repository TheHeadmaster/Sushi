using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies pre-test while loops and nested loop transfers through parsing,
/// binding, structured IR, LLVM emission, and native execution.
/// </summary>
[TestFixture]
public class WhileLoopCompilationTests
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
            int32 value = Divide(0);
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

    [TestCase("int32 value = 0; while (value < 5) { value += 1; } Ensure(value, 5);", TestName = "While Should Repeat Until Condition Becomes False")]
    [TestCase("int32 value = 4; while (value < 4) { Fail(); } Ensure(value, 4);", TestName = "While May Execute Zero Iterations")]
    [TestCase("while (false) { Fail(); }", TestName = "Constant False While Must Skip Its Body")]
    [TestCase("while (true) { break; }", TestName = "Break Should Exit Constant True While")]
    [TestCase("while (false) { }", TestName = "Empty While Body Should Be Valid")]
    [TestCase("int32 value = 1; while (value < 10) { value *= 2; } Ensure(value, 16);", TestName = "Compound Assignment Should Update Loop Condition")]
    [TestCase("int32 value = 0; while (value < 3) { value = value + 1; } Ensure(value, 3);", TestName = "Ordinary Assignment Should Update Loop Condition")]
    [TestCase("bool running = true; int32 count = 0; while (running) { count += 1; running = count < 3; } Ensure(count, 3);", TestName = "Boolean Local Should Control Repeated Iterations")]
    [TestCase("bool flag = false; while (flag and IsPositive(Divide(0))) { Fail(); }", TestName = "While Condition Should Preserve Logical And Short Circuit")]
    [TestCase("while (true or IsPositive(Divide(0))) { break; }", TestName = "While Condition Should Preserve Logical Or Short Circuit")]
    public async Task WhileLoopShouldExecute(string body)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
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

    [TestCase(TestName = "Continue Should Reevaluate Condition And Skip Remaining Body")]
    public async Task ContinueShouldSkipRemainingStatements()
    {
        string root = CreateRoot("""
            int32 index = 0;
            int32 sum = 0;

            while (index < 6)
            {
                index += 1;

                if (index % 2 == 0)
                {
                    continue;
                }

                sum += index;
            }

            Ensure(index, 6);
            Ensure(sum, 9);
            """);

        using TestProject project = await TestProject.Create(root, Helpers);
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

    [TestCase(TestName = "Break Should Not Reevaluate While Condition")]
    public async Task BreakShouldBypassNextConditionEvaluation()
    {
        string root = CreateRoot("""
            int32 divisor = 1;

            while (IsPositive(Divide(divisor)))
            {
                divisor = 0;
                break;
            }

            Ensure(divisor, 0);
            """);

        using TestProject project = await TestProject.Create(root, Helpers);
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

    [TestCase(TestName = "Inner Break Should Exit Only Innermost Loop")]
    public async Task NestedBreakShouldPreserveOuterLoop()
    {
        string root = CreateRoot("""
            int32 outer = 0;
            int32 total = 0;

            while (outer < 3)
            {
                outer += 1;
                int32 inner = 0;

                while (inner < 10)
                {
                    inner += 1;

                    if (inner == 2)
                    {
                        break;
                    }

                    total += 1;
                }
            }

            Ensure(outer, 3);
            Ensure(total, 3);
            """);

        using TestProject project = await TestProject.Create(root, Helpers);
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

    [TestCase(TestName = "Repeated Break Should Exit Two Enclosing Loops")]
    public async Task RepeatedBreakShouldExitOuterLoop()
    {
        string root = CreateRoot("""
            int32 outer = 0;

            while (outer < 4)
            {
                outer += 1;
                int32 inner = 0;

                while (inner < 4)
                {
                    inner += 1;
                    break break;
                }

                Fail();
            }

            Ensure(outer, 1);
            """);

        using TestProject project = await TestProject.Create(root, Helpers);
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

    [TestCase(TestName = "Repeated Continue Should Select Outer Loop Condition")]
    public async Task RepeatedContinueShouldAdvanceOuterLoop()
    {
        string root = CreateRoot("""
            int32 outer = 0;

            while (outer < 3)
            {
                outer += 1;
                int32 inner = 0;

                while (inner < 2)
                {
                    inner += 1;
                    continue continue;
                }

                Fail();
            }

            Ensure(outer, 3);
            """);

        using TestProject project = await TestProject.Create(root, Helpers);
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

    [TestCase(TestName = "Return Inside While Should Exit Enclosing Value Function")]
    public async Task ReturnInsideWhileShouldExitFunction()
    {
        string helper = Helpers + """

            public int32 FindFirstMultiple(int32 divisor)
            {
                int32 value = 1;

                while (value < 10)
                {
                    if (value % divisor == 0)
                    {
                        return value;
                    }

                    value += 1;
                }

                return 0;
            }

            public int32 ConditionalReturn(bool condition)
            {
                int32 value = 0;

                while (condition)
                {
                    value += 1;

                    if (value == 2)
                    {
                        return value;
                    }
                }

                return value;
            }
            """;

        string root = CreateRoot("""
            Ensure(FindFirstMultiple(3), 3);
            Ensure(FindFirstMultiple(5), 5);
            Ensure(ConditionalReturn(true), 2);
            Ensure(ConditionalReturn(false), 0);
            """);

        using TestProject project = await TestProject.Create(root, helper);
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

    [TestCase("break;", TestName = "Break Outside Loop Should Be Rejected")]
    [TestCase("continue;", TestName = "Continue Outside Loop Should Be Rejected")]
    [TestCase("while (true) { break break; }", TestName = "Break Depth Exceeding Available Loops Should Be Rejected")]
    [TestCase("while (true) { continue continue; }", TestName = "Continue Depth Exceeding Available Loops Should Be Rejected")]
    [TestCase("while (true) { while (true) { break break break; } }", TestName = "Triple Break Depth With Only Two Loops Should Be Rejected")]
    [TestCase("while (true) { while (true) { continue continue continue; } }", TestName = "Triple Continue Depth With Only Two Loops Should Be Rejected")]
    [TestCase("while (false) { break break; }", TestName = "Unreachable Invalid Break Should Still Be Rejected")]
    [TestCase("while (false) { continue continue; }", TestName = "Unreachable Invalid Continue Should Still Be Rejected")]
    [TestCase("while (1) { break; }", TestName = "While Condition Must Be Boolean")]
    [TestCase("while (false) { Missing(); }", TestName = "Unreachable Loop Body Must Still Be Bound")]
    [TestCase("while (false) { int32 inner = 1; } Ensure(inner, 1);", TestName = "Loop Local Must Not Escape Its Scope")]
    [TestCase("while (false) { int32 inner = 1; } inner = 2;", TestName = "Out Of Scope Local Cannot Be Assigned")]
    public async Task InvalidWhileShouldProduceDiagnostic(string body)
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
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression) || diagnostic.IsType(ErrorType.UnknownIdentifier));
    }

    [TestCase("while (true) Fail();", TestName = "While Body Requires Braces")]
    [TestCase("while true { break; }", TestName = "While Condition Requires Parentheses")]
    [TestCase("while () { break; }", TestName = "While Condition Cannot Be Missing")]
    [TestCase("while (true) { break }", TestName = "Break Requires Semicolon")]
    [TestCase("while (true) { continue }", TestName = "Continue Requires Semicolon")]
    [TestCase("while (true) { break break }", TestName = "Repeated Break Requires Semicolon")]
    public async Task MalformedWhileShouldProduceSyntaxDiagnostic(string body)
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

    [TestCase(TestName = "Potentially Skipped While Return Should Not Satisfy Value Return Contract")]
    public async Task ConditionalWhileReturnShouldNotSuppressMissingReturnPath()
    {
        string helper = Helpers + """

            public int32 Evaluate(bool condition)
            {
                while (condition)
                {
                    return 1;
                }
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), helper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.MissingReturnPath));
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
                    name = "While Loops"
                    assembly = "WhileLoops"
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