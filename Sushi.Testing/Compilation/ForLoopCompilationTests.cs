using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies three-part for loops through parsing, semantic binding,
/// structured IR, LLVM emission, and native execution.
/// </summary>
[TestFixture]
public class ForLoopCompilationTests
{
    private const string Helpers = """
        package Example;
        namespace Example;

        public int32 Divide(int32 divisor)
        {
            return 10 / divisor;
        }

        public int32 Double(int32 value)
        {
            return value * 2;
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

    [TestCase("int32 total = 0; for (int32 i = 0; i < 5; i += 1) { total += i; } Ensure(total, 10);", TestName = "For Should Sum Five Iterations")]
    [TestCase("int32 total = 0; for (int32 i = 0; i < 5; i++) { total += i; } Ensure(total, 10);", TestName = "Postfix Increment Should Execute In Iteration Clause")]
    [TestCase("int32 total = 0; for (int32 i = 0; i < 5; ++i) { total += i; } Ensure(total, 10);", TestName = "Prefix Increment Should Execute In Iteration Clause")]
    [TestCase("int32 total = 0; for (int32 i = 5; i > 0; i--) { total += i; } Ensure(total, 15);", TestName = "Postfix Decrement Should Execute In Iteration Clause")]
    [TestCase("int32 total = 0; for (int32 i = 5; i > 0; --i) { total += i; } Ensure(total, 15);", TestName = "Prefix Decrement Should Execute In Iteration Clause")]
    [TestCase("int32 total = 0; for (int32 i = 0; i < 0; i++) { total += 1; } Ensure(total, 0);", TestName = "For Body May Execute Zero Times")]
    [TestCase("for (;;) { break; }", TestName = "All Three For Header Clauses May Be Omitted")]
    [TestCase("int32 i = 0; for (; i < 3; i++) { } Ensure(i, 3);", TestName = "For Initialization May Be Omitted")]
    [TestCase("int32 i = 0; for (; ; i++) { if (i == 3) { break; } } Ensure(i, 3);", TestName = "For Condition May Be Omitted")]
    [TestCase("int32 i = 0; for (; i < 3; ) { i += 1; } Ensure(i, 3);", TestName = "For Iteration May Be Omitted")]
    [TestCase("for (int32 i = 0; i < 3; i++) { }", TestName = "Empty For Body Should Be Valid")]
    [TestCase("int32 value = 0; for (value = 1; value < 8; value *= 2) { } Ensure(value, 8);", TestName = "For Initialization Can Assign Existing Local")]
    [TestCase("int32 value = 0; for (value++; value < 3; value++) { } Ensure(value, 3);", TestName = "For Initialization Can Use Increment")]
    [TestCase("int32 total = 0; for (int32 i = 0; i < 4; i += Double(1)) { total += 1; } Ensure(total, 2);", TestName = "Iteration May Use Value Returning Calls")]
    public async Task ForLoopShouldExecute(string body)
    {
        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Initialization Declarations Must Execute Sequentially And Be Visible To Later Initializers")]
    public async Task ForShouldSupportIndependentCommaSeparatedDeclarations()
    {
        await AssertNativeSuccess("""
            int32 total = 0;

            for (int32 left = 1, int32 right = left + 1; left < 3; left++, right += 2)
            {
                total += right;
            }

            Ensure(total, 6);
            """);
    }

    [TestCase(TestName = "For Initialization May Contain Mixed Concrete Types")]
    public async Task ForShouldSupportMixedHeaderDeclarations()
    {
        await AssertNativeSuccess("""
            int32 total = 0;

            for (int32 index = 0, bool running = true; running; index += 1, running = index < 3)
            {
                total += index;
            }

            Ensure(total, 3);
            """);
    }

    [TestCase(TestName = "Iteration Operations Must Execute From Left To Right")]
    public async Task ForShouldPreserveIterationOrder()
    {
        await AssertNativeSuccess("""
            int32 first = 1;
            int32 second = 0;

            for (; first < 4; second = first, first += 1)
            {
            }

            Ensure(first, 4);
            Ensure(second, 3);
            """);
    }

    [TestCase(TestName = "Continue Must Execute Iteration Clause Before Condition")]
    public async Task ContinueShouldExecuteIterationClause()
    {
        await AssertNativeSuccess("""
            int32 total = 0;

            for (int32 i = 0; i < 6; i++)
            {
                if (i % 2 == 0)
                {
                    continue;
                }

                total += i;
            }

            Ensure(total, 9);
            """);
    }

    [TestCase(TestName = "Break Must Skip Iteration Operations")]
    public async Task BreakShouldSkipIterationClause()
    {
        await AssertNativeSuccess("""
            int32 value = 0;
            int32 divisor = 1;

            for (; value < 4; value += Divide(divisor))
            {
                value += 1;
                divisor = 0;
                break;
            }

            Ensure(value, 1);
            """);
    }

    [TestCase(TestName = "Repeated Continue From Inner While Must Execute Outer For Iteration")]
    public async Task RepeatedContinueShouldTargetOuterForIteration()
    {
        await AssertNativeSuccess("""
            int32 count = 0;

            for (int32 index = 0; index < 4; index++)
            {
                while (true)
                {
                    count += 1;
                    continue continue;
                }

                Fail();
            }

            Ensure(count, 4);
            """);
    }

    [TestCase(TestName = "Repeated Continue From Inner Do While Must Execute Outer For Iteration")]
    public async Task RepeatedContinueFromDoWhileShouldTargetOuterFor()
    {
        await AssertNativeSuccess("""
            int32 count = 0;

            for (int32 index = 0; index < 3; index++)
            {
                do
                {
                    count += 1;
                    continue continue;
                } while (false)

                Fail();
            }

            Ensure(count, 3);
            """);
    }

    [TestCase(TestName = "Repeated Break Must Exit Outer For And Skip Its Iteration")]
    public async Task RepeatedBreakShouldExitOuterFor()
    {
        await AssertNativeSuccess("""
            int32 index = 0;

            for (; index < 4; index++)
            {
                while (true)
                {
                    index += 1;
                    break break;
                }

                Fail();
            }

            Ensure(index, 1);
            """);
    }

    [TestCase(TestName = "Nested For Loops Must Maintain Independent Initialization Storage")]
    public async Task NestedForLoopsShouldExecute()
    {
        await AssertNativeSuccess("""
            int32 total = 0;

            for (int32 outer = 0; outer < 3; outer++)
            {
                for (int32 inner = 0; inner < 2; inner++)
                {
                    total += 1;
                }
            }

            Ensure(total, 6);
            """);
    }

    [TestCase(TestName = "Return Inside For Must Exit Enclosing Value Function")]
    public async Task ReturnInsideForShouldExitFunction()
    {
        string functions = Helpers + """

            public int32 Find(int32 target)
            {
                for (int32 i = 0; i < 10; i++)
                {
                    if (i == target)
                    {
                        return i;
                    }
                }

                return -1;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Ensure(Find(4), 4); Ensure(Find(20), -1);"), functions);
        MVPCompilationResult result = await project.Compile();

        result.Diagnostics
            .Should()
            .BeEmpty();

        result.Succeeded
            .Should()
            .BeTrue();

        (await RunExecutable(result.Executable!.FilePath))
            .Should()
            .Be(0);
    }

    [TestCase("for (; 1; ) { break; }", ErrorType.UnsupportedMVPExpression, TestName = "For Condition Must Be Boolean")]
    [TestCase("for (int32 i = 0; i < 3; i++) { } i = 1;", ErrorType.UnknownIdentifier, TestName = "For Initializer Local Must Not Escape Header Scope")]
    [TestCase("for (int32 i = 0; i < 3; i++) { } Ensure(i, 3);", ErrorType.UnknownIdentifier, TestName = "For Initializer Local Must Not Be Read After Loop")]
    [TestCase("for (; inner < 3; ) { int32 inner = 1; }", ErrorType.UnknownIdentifier, TestName = "Loop Body Declaration Must Not Be Accessible In Header")]
    [TestCase("for (int32 i = 0; i < 3; i++) { break break; }", ErrorType.UnsupportedMVPExpression, TestName = "Break Depth Must Not Exceed Enclosing Loops")]
    [TestCase("for (int32 i = 0; i < 3; i++) { continue continue; }", ErrorType.UnsupportedMVPExpression, TestName = "Continue Depth Must Not Exceed Enclosing Loops")]
    [TestCase("for (bool value = true; value; value++) { break; }", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Increment Must Be Rejected")]
    [TestCase("for (bool value = true; value; --value) { break; }", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Decrement Must Be Rejected")]
    [TestCase("for (missing = 0; false; ) { }", ErrorType.UnknownIdentifier, TestName = "Unknown Initialization Target Must Be Rejected")]
    [TestCase("for (int32 index = 0; false; index += true) { }", ErrorType.UnsupportedMVPExpression, TestName = "Unreachable Iteration Must Still Be Type Checked")]
    [TestCase("for (int32 index = 0; false; index++) { Missing(); }", ErrorType.UnsupportedMVPExpression, TestName = "Unreachable For Body Must Still Be Bound")]
    public async Task InvalidForShouldProduceDiagnostic(string body, ErrorType expectedError)
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

    [TestCase("for (int32 i = 0; i < 3; i += 1) Ensure(i, 1);", TestName = "For Body Requires Braces")]
    [TestCase("for (int32 i = 0; i < 3; i++) { } ;", TestName = "For Statement Has No Trailing Semicolon")]
    [TestCase("for (int32 i = 0; i < 3; i++,) { }", TestName = "Trailing Iteration Comma Must Be Rejected")]
    [TestCase("for (int32 i = 0,; i < 3; i++) { }", TestName = "Trailing Initialization Comma Must Be Rejected")]
    [TestCase("for (int32 i = 0; i < 3; Double(i)) { }", TestName = "Function Call Alone Is Not Valid Iteration Operation")]
    [TestCase("for (int32 i = 0; i < 3; int32 j = 1) { }", TestName = "Iteration Clause Cannot Declare Local")]
    [TestCase("for (int32 i = 0; i < 3; i++) { } i++;", TestName = "Standalone Increment Statements Are Not Introduced By This Slice")]
    public async Task MalformedForShouldProduceSyntaxDiagnostic(string body)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "For Loop With Potential Fallthrough Must Not Satisfy Required Return")]
    public async Task ForShouldPreserveConservativeReturnAnalysis()
    {
        string functions = Helpers + """

            public int32 Invalid(bool flag)
            {
                for (; flag; )
                {
                    return 1;
                }
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

        (await RunExecutable(result.Executable!.FilePath))
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
                    name = "For Loops"
                    assembly = "ForLoops"
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