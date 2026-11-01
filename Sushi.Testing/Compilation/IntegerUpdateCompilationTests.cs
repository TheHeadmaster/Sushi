using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies prefix and postfix integer updates through parsing, semantic
/// binding, structured IR, LLVM emission, and native execution.
/// </summary>
[TestFixture]
public class IntegerUpdateCompilationTests
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

    [TestCase("int32 value = 10; int32 old = value++; Ensure(old, 10); Ensure(value, 11);", TestName = "Postfix Increment Produces Previous Value")]
    [TestCase("int32 value = 10; int32 next = ++value; Ensure(next, 11); Ensure(value, 11);", TestName = "Prefix Increment Produces New Value")]
    [TestCase("int32 value = 10; int32 old = value--; Ensure(old, 10); Ensure(value, 9);", TestName = "Postfix Decrement Produces Previous Value")]
    [TestCase("int32 value = 10; int32 next = --value; Ensure(next, 9); Ensure(value, 9);", TestName = "Prefix Decrement Produces New Value")]
    [TestCase("int32 value = 10; value++; Ensure(value, 11);", TestName = "Postfix Increment Is Valid Standalone Statement")]
    [TestCase("int32 value = 10; ++value; Ensure(value, 11);", TestName = "Prefix Increment Is Valid Standalone Statement")]
    [TestCase("int32 value = 10; value--; Ensure(value, 9);", TestName = "Postfix Decrement Is Valid Standalone Statement")]
    [TestCase("int32 value = 10; --value; Ensure(value, 9);", TestName = "Prefix Decrement Is Valid Standalone Statement")]
    [TestCase("int32 value = 10; (++value); Ensure(value, 11);", TestName = "Parenthesized Update Is Valid Standalone Statement")]
    [TestCase("int32 value = 10; ++(value); Ensure(value, 11);", TestName = "Parenthesized Identifier Preserves Writable Location")]
    [TestCase("int32 value = 10; (value)++; Ensure(value, 11);", TestName = "Parenthesized Identifier Supports Postfix Update")]
    [TestCase("int32 value = 10; int32 result = ++value + 5; Ensure(result, 16); Ensure(value, 11);", TestName = "Prefix Update Works In Arithmetic")]
    [TestCase("int32 value = 10; int32 result = value++ * 2; Ensure(result, 20); Ensure(value, 11);", TestName = "Postfix Update Binds Before Multiplication")]
    [TestCase("int32 value = 10; int32 result = -value++; Ensure(result, -10); Ensure(value, 11);", TestName = "Postfix Update Binds Before Unary Negation")]
    [TestCase("int32 value = 1; int32 result = value++ + ++value; Ensure(result, 4); Ensure(value, 3);", TestName = "Binary Operands Update In Left To Right Order")]
    [TestCase("int32 value = 1; int32 result = value++ + value++; Ensure(result, 3); Ensure(value, 3);", TestName = "Repeated Postfix Updates Each Read Current Value")]
    [TestCase("int32 value = 5; int32 result = Double(value++); Ensure(result, 10); Ensure(value, 6);", TestName = "Postfix Update In Call Argument Produces Previous Value")]
    [TestCase("int32 value = 5; int32 result = Double(++value); Ensure(result, 12); Ensure(value, 6);", TestName = "Prefix Update In Call Argument Produces New Value")]
    [TestCase("int32 value = 5; value = value++; Ensure(value, 5);", TestName = "Assignment Applies RHS Result After Update")]
    [TestCase("int32 value = 5; value += value++; Ensure(value, 10);", TestName = "Compound Assignment Preserves Left Read Before RHS Update")]
    [TestCase("int32 value = 2; Require(value++ < 3); Ensure(value, 3);", TestName = "Postfix Update Works In Integer Comparison")]
    [TestCase("int32 value = 2; Require(++value == 3); Ensure(value, 3);", TestName = "Prefix Update Works In Integer Equality")]
    [TestCase("int32 value = 2; bool result = false and (value++ > 0); Require(not result); Ensure(value, 2);", TestName = "Short Circuit And Skips Update")]
    [TestCase("int32 value = 2; bool result = true or (value++ > 0); Require(result); Ensure(value, 2);", TestName = "Short Circuit Or Skips Update")]
    [TestCase("int32 value = 2; bool result = false or (value++ > 0); Require(result); Ensure(value, 3);", TestName = "Logical Or Evaluates Required Update")]
    [TestCase("int32 value = 2; bool result = true and (++value > 2); Require(result); Ensure(value, 3);", TestName = "Logical And Evaluates Required Update")]
    [TestCase("int32 value = 1; int32 previous = value++; int32 next = ++value; int32 before = value--; int32 after = --value; Ensure(previous, 1); Ensure(next, 3); Ensure(before, 3); Ensure(after, 1); Ensure(value, 1);", TestName = "All Four Update Forms Compose")]
    public async Task IntegerUpdatesShouldExecute(string body)
    {
        await AssertNativeSuccess(body);
    }

    [TestCase(TestName = "While Condition Should Preserve Postfix Update And Retest")]
    public async Task PostfixUpdateShouldWorkInWhileCondition()
    {
        await AssertNativeSuccess("""
            int32 index = 0;
            int32 count = 0;

            while (index++ < 3)
            {
                count += 1;
            }

            Ensure(index, 4);
            Ensure(count, 3);
            """);
    }

    [TestCase(TestName = "For Condition And Iteration Should Both Use Update Semantics")]
    public async Task UpdatesShouldWorkAcrossForPhases()
    {
        await AssertNativeSuccess("""
            int32 count = 0;

            for (int32 index = 0; index++ < 3; )
            {
                count += 1;
            }

            Ensure(count, 3);

            int32 total = 0;

            for (int32 value = 0; value < 3; ++value)
            {
                total += value;
            }

            Ensure(total, 3);
            """);
    }

    [TestCase(TestName = "For Header Updates Should Use The Same Checked Update Operations")]
    public async Task HeaderUpdatesShouldRemainCompatible()
    {
        await AssertNativeSuccess("""
            int32 count = 0;

            for (int32 index = 3; index > 0; index--)
            {
                count += 1;
            }

            for (int32 index = 0; index < 2; ++index)
            {
                count += 1;
            }

            Ensure(count, 5);
            """);
    }

    [TestCase(TestName = "Update In Value Returning Function Should Produce Correct Value")]
    public async Task UpdatesShouldWorkInReturnExpressions()
    {
        string functions = Helpers + """

            public int32 ReturnOld()
            {
                int32 value = 7;
                return value++;
            }

            public int32 ReturnNew()
            {
                int32 value = 7;
                return ++value;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Ensure(ReturnOld(), 7); Ensure(ReturnNew(), 8);"), functions);
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

    [TestCase("int32 value = 1; int32 result = ++(value + 1);", ErrorType.UnsupportedMVPExpression, TestName = "Arithmetic Result Is Not Writable Storage")]
    [TestCase("int32 value = 1; int32 result = ++value++;", ErrorType.UnsupportedMVPExpression, TestName = "Prefix Cannot Update Result Of Postfix")]
    [TestCase("int32 value = 1; int32 result = value++++;", ErrorType.UnsupportedMVPExpression, TestName = "Postfix Cannot Update Result Of Postfix")]
    [TestCase("int32 value = 1; int32 result = ++++value;", ErrorType.UnsupportedMVPExpression, TestName = "Prefix Cannot Update Result Of Prefix")]
    [TestCase("int32 value = 1; int32 result = (value++)++;", ErrorType.UnsupportedMVPExpression, TestName = "Parentheses Do Not Make Update Result Writable")]
    [TestCase("int32 value = 1; int32 result = 42++;", ErrorType.UnsupportedMVPExpression, TestName = "Integer Literal Cannot Be Updated")]
    [TestCase("int32 value = 1; int32 result = ++42;", ErrorType.UnsupportedMVPExpression, TestName = "Integer Literal Cannot Be Prefix Updated")]
    [TestCase("int32 value = 1; int32 result = ++Double(value);", ErrorType.UnsupportedMVPExpression, TestName = "Function Result Cannot Be Updated")]
    [TestCase("bool flag = true; ++flag;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Local Cannot Be Incremented")]
    [TestCase("bool flag = true; flag--;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Local Cannot Be Decremented")]
    [TestCase("++missing;", ErrorType.UnknownIdentifier, TestName = "Unknown Prefix Target Is Diagnosed")]
    [TestCase("missing++;", ErrorType.UnknownIdentifier, TestName = "Unknown Postfix Target Is Diagnosed")]
    [TestCase("int32 value = 1; ++value + 1;", ErrorType.UnsupportedMVPExpression, TestName = "Arbitrary Arithmetic Expression Is Not A Standalone Statement")]
    [TestCase("int32 value = 1; (value + 1);", ErrorType.UnsupportedMVPExpression, TestName = "Parenthesized Arithmetic Is Not A Standalone Statement")]
    public async Task InvalidIntegerUpdateShouldProduceDiagnostic(string body, ErrorType expectedError)
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

    [TestCase(TestName = "Function Parameters Are Not Writable In The Current Subset")]
    public async Task UpdatingParameterShouldBeRejected()
    {
        string functions = Helpers + """

            public int32 Invalid(int32 value)
            {
                return value++;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), functions);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase("int32 value = 2147483647; value++;", TestName = "Postfix Increment Overflow Must Fail")]
    [TestCase("int32 value = 2147483647; ++value;", TestName = "Prefix Increment Overflow Must Fail")]
    [TestCase("int32 value = -2147483648; value--;", TestName = "Postfix Decrement Underflow Must Fail")]
    [TestCase("int32 value = -2147483648; --value;", TestName = "Prefix Decrement Underflow Must Fail")]
    public async Task IntegerUpdateOverflowShouldFailAtRuntime(string body)
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

    [TestCase("int32 value = 1; value++", TestName = "Standalone Update Requires Semicolon")]
    [TestCase("int32 value = 1; ++value", TestName = "Prefix Statement Requires Semicolon")]
    [TestCase("int32 value = 1; int32 result = value++", TestName = "Update Initializer Declaration Requires Semicolon")]
    public async Task MissingUpdateTerminatorShouldProduceSyntaxDiagnostic(string body)
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
                    name = "Integer Updates"
                    assembly = "IntegerUpdates"
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