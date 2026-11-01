using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Exercises interactions between mutable local storage, side-effecting
/// expressions, compound assignments, function calls, and loop control flow
/// through the complete native compilation pipeline.
/// </summary>
[TestFixture]
public class ControlFlowUpdateIntegrationTests
{
    private const string Helpers = """
        package Example;
        namespace Example;

        public int32 Divide(int32 divisor)
        {
            return 10 / divisor;
        }

        public int32 Scale(int32 value)
        {
            return value * 10;
        }

        public int32 Subtract(int32 left, int32 right)
        {
            return left - right;
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

    [TestCase(
        "int32 value = 5; value += value++ + ++value; Ensure(value, 17);",
        TestName = "Compound Assignment Reads Original Target Before Side Effecting RHS")]
    [TestCase(
        "int32 value = 5; value = value++ + ++value; Ensure(value, 12);",
        TestName = "Ordinary Assignment Stores Final RHS Result After Updates")]
    [TestCase(
        "int32 value = 1; int32 result = Scale(value++) + (value++ * ++value); Ensure(result, 18); Ensure(value, 4);",
        TestName = "Nested Arithmetic And Calls Preserve Left To Right Updates")]
    [TestCase(
        "int32 value = 1; int32 result = Subtract(right: value++, left: ++value); Ensure(result, 2); Ensure(value, 3);",
        TestName = "Named Arguments Evaluate In Source Order Rather Than Parameter Order")]
    [TestCase(
        "int32 shift = 1; int32 value = 2; value <<= shift++; Ensure(value, 4); Ensure(shift, 2);",
        TestName = "Compound Shift Evaluates Its Runtime Count Exactly Once")]
    [TestCase(
        "int32 value = 7; bool high = value++ < 9999999999999999999999999999; Require(high); Ensure(value, 8);",
        TestName = "Out Of Range Exact Comparison Still Evaluates Its Runtime Operand")]
    [TestCase(
        "int32 value = 7; bool low = -9999999999999999999999999999 < value++; Require(low); Ensure(value, 8);",
        TestName = "Reversed Exact Comparison Still Evaluates Its Runtime Operand")]
    public async Task CombinedUpdatesShouldPreserveEvaluationOrder(string body)
    {
        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "Nested Short Circuit Operators Must Skip Only Unselected Updates")]
    public async Task NestedShortCircuitShouldPreserveMutationsAndPhiResults()
    {
        await AssertNativeSuccess("""
            int32 value = 2;
            bool enabled = false;

            bool first = enabled and (value++ > 0);
            Require(not first);
            Ensure(value, 2);

            bool second = first or (value++ == 2);
            Require(second);
            Ensure(value, 3);

            bool third = second or (++value > 0);
            Require(third);
            Ensure(value, 3);

            bool fourth = second and ((value++ > 1) or (value++ > 100));
            Require(fourth);
            Ensure(value, 4);
            """);
    }

    [TestCase(TestName = "Eager Boolean Operators Must Evaluate Both Operands")]
    public async Task EagerBooleanOperatorsShouldNotShortCircuitUpdates()
    {
        await AssertNativeSuccess("""
            int32 value = 0;

            bool first = false & (value++ >= 0);
            Require(not first);
            Ensure(value, 1);

            bool second = true | (value++ > 0);
            Require(second);
            Ensure(value, 2);

            bool third = true ^ (value++ == 2);
            Require(not third);
            Ensure(value, 3);
            """);
    }

    [TestCase(TestName = "Conditional Branches Must Select Correct Side Effects")]
    public async Task ConditionalBranchesShouldExecuteOnlySelectedUpdates()
    {
        await AssertNativeSuccess("""
            int32 value = 0;

            if (value++ == 0)
            {
                value += 2;
            }
            else
            {
                value += 100;
            }

            Ensure(value, 3);

            if (value++ == 3)
            {
                value += 4;
            }
            else
            {
                Fail();
            }

            Ensure(value, 8);
            """);
    }

    [TestCase(TestName = "While Continue Must Retest A Side Effecting Condition")]
    public async Task WhileShouldReevaluateUpdateConditionAfterContinue()
    {
        await AssertNativeSuccess("""
            int32 index = 0;
            int32 total = 0;

            while (index++ < 5)
            {
                if (index % 2 == 0)
                {
                    continue;
                }

                total += index;
            }

            Ensure(index, 6);
            Ensure(total, 9);
            """);
    }

    [TestCase(TestName = "Do While Continue Must Reach Trailing Side Effecting Condition")]
    public async Task DoWhileShouldEvaluateTrailingUpdateAfterContinue()
    {
        await AssertNativeSuccess("""
            int32 index = 0;
            int32 visits = 0;

            do
            {
                visits++;

                if (visits == 1)
                {
                    continue;
                }
            } while (index++ < 2)

            Ensure(visits, 3);
            Ensure(index, 3);
            """);
    }

    [TestCase(TestName = "Do While Break Must Skip Dangerous Trailing Condition")]
    public async Task DoWhileBreakShouldNotEvaluateTrailingCondition()
    {
        await AssertNativeSuccess("""
            int32 divisor = 1;

            do
            {
                divisor = 0;
                break;
            } while (Divide(divisor) > 0)

            Ensure(divisor, 0);
            """);
    }

    [TestCase(TestName = "For Continue Must Preserve Ordered Side Effecting Iteration Operations")]
    public async Task ForContinueShouldExecuteIterationUpdatesInOrder()
    {
        await AssertNativeSuccess("""
            int32 first = 0;
            int32 second = 0;

            for (; first < 3; second += first++, ++second)
            {
                if (first < 2)
                {
                    continue;
                }
            }

            Ensure(first, 3);
            Ensure(second, 6);
            """);
    }

    [TestCase(TestName = "Nested Continue Must Run Outer For Iteration But Skip Inner Iteration")]
    public async Task NestedContinueShouldTargetOuterForIteration()
    {
        await AssertNativeSuccess("""
            int32 outer = 0;
            int32 visits = 0;

            for (; outer < 3; outer++)
            {
                for (int32 inner = 0; inner < 4; inner++)
                {
                    visits++;
                    continue continue;
                }

                Fail();
            }

            Ensure(outer, 3);
            Ensure(visits, 3);
            """);
    }

    [TestCase(TestName = "Triple Continue Must Reach The Outer While Condition")]
    public async Task TripleContinueShouldPreserveMixedLoopTargets()
    {
        await AssertNativeSuccess("""
            int32 cursor = 0;
            int32 visits = 0;

            while (cursor++ < 3)
            {
                do
                {
                    for (int32 inner = 0; inner < 2; inner++)
                    {
                        visits++;
                        continue continue continue;
                    }

                    Fail();
                } while (false)

                Fail();
            }

            Ensure(cursor, 4);
            Ensure(visits, 3);
            """);
    }

    [TestCase(TestName = "Short Circuit In A Loop Must Avoid A Dangerous Runtime Call")]
    public async Task LoopConditionShouldSkipUnselectedFailingCall()
    {
        await AssertNativeSuccess("""
            int32 value = 0;
            int32 divisor = 0;
            bool enabled = false;

            while (enabled and (Divide(divisor) > 0))
            {
                Fail();
            }

            for (; value < 3; value++)
            {
                bool shouldContinue = (value++ < 1) or enabled;

                if (shouldContinue)
                {
                    continue;
                }

                break;
            }

            Ensure(value, 3);
            """);
    }

    [TestCase(TestName = "Value Returns From Loops Must Preserve Prefix And Postfix Results")]
    public async Task ValueReturningFunctionsShouldPreserveUpdateSemantics()
    {
        string functions = Helpers + """

            public int32 Evaluate(bool shouldStop)
            {
                int32 value = 0;

                while (value++ < 5)
                {
                    if (shouldStop and value == 3)
                    {
                        return ++value;
                    }
                }

                return value;
            }
            """;

        await AssertNativeSuccess("""
            Ensure(Evaluate(true), 4);
            Ensure(Evaluate(false), 6);
            """, functions);
    }

    [TestCase("if (false and (missing++ > 0)) { }", ErrorType.UnknownIdentifier,
        TestName = "Short Circuited Invalid Update Must Still Be Diagnosed")]
    [TestCase("while (false) { ++missing; }", ErrorType.UnknownIdentifier,
        TestName = "Unreachable Loop Body Update Must Still Be Bound")]
    [TestCase("for (int32 index = 0; false; index++) { int32 value = 0; ++(value + 1); }", ErrorType.UnsupportedMVPExpression,
        TestName = "Unreachable Loop Body Must Reject Non Writable Update Operand")]
    [TestCase("bool flag = false; flag |= (missing++ > 0);", ErrorType.UnknownIdentifier,
        TestName = "Eager Boolean Assignment Must Reject Unknown Update Target")]
    public async Task InvalidCombinedExpressionShouldStillBeDiagnosed(string body, ErrorType error)
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
            .Contain(diagnostic => diagnostic.IsType(error));
    }

    [TestCase("int32 divisor = 1; int32 value = Divide(--divisor);",
        TestName = "Side Effecting Division By Zero Must Fail At Runtime")]
    [TestCase("int32 value = 2147483647; value += ++value;",
        TestName = "Compound Update Overflow Must Fail At Runtime")]
    public async Task RuntimeFailureShouldRemainObservable(string body)
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
            .NotBe(0);
    }

    private static async Task AssertNativeSuccess(string body, string? helperSource = null)
    {
        using TestProject project = await TestProject.Create(CreateRoot(body), helperSource ?? Helpers);
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

            throw new TimeoutException("Compiled Sushi program did not terminate within the test timeout.");
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
                    name = "Control Flow Update Integration"
                    assembly = "ControlFlowUpdateIntegration"
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