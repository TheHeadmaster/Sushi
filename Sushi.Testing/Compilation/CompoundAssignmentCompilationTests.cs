using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies compound assignments through lexical recognition, parsing,
/// semantic binding, structured IR, LLVM emission, and native execution.
/// </summary>
[TestFixture]
public class CompoundAssignmentCompilationTests
{
    private const string Helpers = """
        package Example;
        namespace Example;

        public int32 Double(int32 value)
        {
            return value * 2;
        }

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

    [TestCase("int32 value = 10; value += 5; Ensure(value, 15);", TestName = "Compound Addition Should Execute")]
    [TestCase("int32 value = 10; value -= 3; Ensure(value, 7);", TestName = "Compound Subtraction Should Execute")]
    [TestCase("int32 value = 6; value *= 7; Ensure(value, 42);", TestName = "Compound Multiplication Should Execute")]
    [TestCase("int32 value = 20; value /= 4; Ensure(value, 5);", TestName = "Compound Division Should Execute")]
    [TestCase("int32 value = 20; value %= 6; Ensure(value, 2);", TestName = "Compound Remainder Should Execute")]
    [TestCase("int32 value = 1; value <<= 3; Ensure(value, 8);", TestName = "Compound Left Shift Should Execute")]
    [TestCase("int32 value = 32; value >>= 2; Ensure(value, 8);", TestName = "Compound Right Shift Should Execute")]
    [TestCase("int32 value = 14; value &= 10; Ensure(value, 10);", TestName = "Compound Integer Bitwise And Should Execute")]
    [TestCase("int32 value = 10; value ^= 3; Ensure(value, 9);", TestName = "Compound Integer Bitwise Xor Should Execute")]
    [TestCase("int32 value = 10; value |= 5; Ensure(value, 15);", TestName = "Compound Integer Bitwise Or Should Execute")]
    [TestCase("bool flag = true; flag &= false; Require(not flag);", TestName = "Compound Boolean And Should Execute")]
    [TestCase("bool flag = false; flag |= true; Require(flag);", TestName = "Compound Boolean Or Should Execute")]
    [TestCase("bool flag = true; flag ^= true; Require(not flag);", TestName = "Compound Boolean Xor Should Execute")]
    [TestCase("int32 value = 5; value += Double(value); Ensure(value, 15);", TestName = "Compound RHS Should Read Prior Value")]
    [TestCase("int32 value = 2; value += 3; value *= 4; value -= 5; Ensure(value, 15);", TestName = "Compound Operations Should Compose In Source Order")]
    [TestCase("int32 value = 2; value+=3; value<<=1; Ensure(value, 10);", TestName = "Compound Operators Should Not Require Whitespace")]
    [TestCase("int32 value = -16; value >>= 2; Ensure(value, -4);", TestName = "Right Shift Should Preserve Sign")]
    [TestCase("int32 value = 3; value <<= 0; Ensure(value, 3);", TestName = "Zero Shift Should Be Valid")]
    [TestCase("int32 value = 10; value /= -3; Ensure(value, -3);", TestName = "Signed Division Should Truncate Toward Zero")]
    [TestCase("int32 value = 10; value %= -3; Ensure(value, 1);", TestName = "Signed Remainder Should Preserve Dividend Sign")]
    [TestCase("int32 value = 1; value = 4; value += 5; Ensure(value, 9);", TestName = "Ordinary And Compound Assignment Should Interoperate")]
    [TestCase("int32 value = 10; value += 1 + 2; Ensure(value, 13);", TestName = "Compound RHS Should Respect Expression Precedence")]
    [TestCase("int32 value = 1; value <<= 1 + 2; Ensure(value, 8);", TestName = "Compound Shift Should Accept Exact Constant Count Expressions")]
    [TestCase("int32 value = 1; int32 count = 3; value <<= count; Ensure(value, 8);", TestName = "Compound Shift Should Accept Runtime Count")]
    [TestCase("bool flag = false; flag |= IsPositive(Double(1)); Require(flag);", TestName = "Boolean Compound RHS Should Accept Value Call")]
    public async Task CompoundAssignmentShouldExecute(string body)
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

    [TestCase(TestName = "Compound Assignments Should Preserve Values Across Dynamic Branches")]
    public async Task CompoundAssignmentShouldPreserveConditionalStorage()
    {
        string functions = Helpers + """

            public int32 Calculate(bool condition)
            {
                int32 result = 10;

                if (condition)
                {
                    result += 5;
                    result *= 2;
                }
                else
                {
                    result -= 3;
                    result <<= 1;
                }

                return result;
            }

            public bool Combine(bool first, bool second)
            {
                bool result = first;

                if (second)
                {
                    result ^= true;
                }
                else
                {
                    result |= false;
                }

                return result;
            }

            public int32 Nested(bool outer, bool inner)
            {
                int32 result = 1;

                if (outer)
                {
                    result += 2;

                    if (inner)
                    {
                        result *= 4;
                    }
                }
                else if (inner)
                {
                    result <<= 3;
                }

                return result;
            }
            """;

        string root = CreateRoot("""
            Ensure(Calculate(true), 30);
            Ensure(Calculate(false), 14);

            Require(not Combine(true, true));
            Require(Combine(true, false));
            Require(Combine(false, true));
            Require(not Combine(false, false));

            Ensure(Nested(true, true), 12);
            Ensure(Nested(true, false), 3);
            Ensure(Nested(false, true), 8);
            Ensure(Nested(false, false), 1);
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

    [TestCase("int32 value = 10; value /= 0;", ErrorType.IntegerDivisionByZero, TestName = "Constant Zero Compound Divisor Should Be Rejected")]
    [TestCase("int32 value = 10; value %= (4 - 4);", ErrorType.IntegerDivisionByZero, TestName = "Calculated Zero Compound Divisor Should Be Rejected")]
    [TestCase("int32 value = 1; value <<= -1;", ErrorType.InvalidIntegerShiftCount, TestName = "Negative Left Shift Count Should Be Rejected")]
    [TestCase("int32 value = 1; value >>= 32;", ErrorType.InvalidIntegerShiftCount, TestName = "Excessive Right Shift Count Should Be Rejected")]
    [TestCase("int32 value = 1; value <<= (1 << 5);", ErrorType.InvalidIntegerShiftCount, TestName = "Calculated Excessive Shift Count Should Be Rejected")]
    [TestCase("int32 value = 1; value <<= 2147483648;", ErrorType.InvalidIntegerShiftCount, TestName = "Exact Untyped Shift Count Should Be Validated Before Narrowing")]
    [TestCase("bool flag = true; flag += true;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Addition Assignment Should Be Rejected")]
    [TestCase("bool flag = true; flag <<= 1;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Shift Assignment Should Be Rejected")]
    [TestCase("int32 value = 1; value &= true;", ErrorType.UnsupportedMVPExpression, TestName = "Integer Bitwise Assignment Should Reject Boolean RHS")]
    [TestCase("bool flag = true; flag &= 1;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Bitwise Assignment Should Reject Integer RHS")]
    [TestCase("int32 value = 1; value += IsPositive(1);", ErrorType.UnsupportedMVPExpression, TestName = "Compound Assignment Should Reject Mismatched Call Result")]
    [TestCase("missing += 1;", ErrorType.UnknownIdentifier, TestName = "Unknown Compound Target Should Be Rejected")]
    [TestCase("value += 1; int32 value = 1;", ErrorType.UnknownIdentifier, TestName = "Compound Assignment Before Declaration Should Be Rejected")]
    [TestCase("if (true) { int32 inner = 1; } inner += 1;", ErrorType.UnknownIdentifier, TestName = "Out Of Scope Compound Target Should Be Rejected")]
    public async Task InvalidCompoundAssignmentShouldProduceDiagnostic(string body, ErrorType expectedError)
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

    [TestCase(TestName = "Compound Assignment To Parameter Should Be Rejected")]
    public async Task ParameterCompoundAssignmentShouldBeRejected()
    {
        string functions = Helpers + """

            public int32 Invalid(int32 value)
            {
                value += 1;
                return value;
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

    [TestCase("int32 value = 1; value += ;", TestName = "Compound Assignment Missing RHS Should Be Rejected")]
    [TestCase("int32 value = 1; value += 2", TestName = "Compound Assignment Missing Semicolon Should Be Rejected")]
    [TestCase("int32 value = 1; value += value += 2;", TestName = "Compound Assignment Chaining Should Be Rejected")]
    [TestCase("int32 value = 1; Ensure(value += 2, 3);", TestName = "Compound Assignment Must Not Be An Expression")]
    [TestCase("int32 value = 1; value and= true;", TestName = "Logical And Assignment Should Not Exist")]
    [TestCase("bool flag = true; flag or= false;", TestName = "Logical Or Assignment Should Not Exist")]
    public async Task MalformedCompoundAssignmentShouldProduceSyntaxDiagnostic(string body)
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

    [TestCase("bool flag = false; flag &= IsPositive(Divide(0));", TestName = "Boolean Compound And Must Eagerly Evaluate RHS")]
    [TestCase("bool flag = true; flag |= IsPositive(Divide(0));", TestName = "Boolean Compound Or Must Eagerly Evaluate RHS")]
    public async Task BooleanCompoundAssignmentShouldEvaluateRightHandSide(string body)
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
            .NotBe(0);
    }

    [TestCase("int32 value = 2147483647; value += 1;", TestName = "Compound Addition Overflow Must Fail At Runtime")]
    [TestCase("int32 value = 1; int32 count = -1; value <<= count;", TestName = "Invalid Runtime Compound Shift Count Must Fail")]
    [TestCase("int32 value = 10; int32 divisor = 0; value /= divisor;", TestName = "Runtime Compound Division By Zero Must Fail")]
    public async Task InvalidRuntimeCompoundAssignmentShouldFail(string body)
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
            .NotBe(0);
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
                    name = "Compound Assignment"
                    assembly = "CompoundAssignment"
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