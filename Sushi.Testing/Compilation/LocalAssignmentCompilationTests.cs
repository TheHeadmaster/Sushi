using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies ordinary assignment to initialized locals through parsing, semantic
/// binding, structured IR, native lowering, linking, and executable behavior.
/// </summary>
[TestFixture]
public class LocalAssignmentCompilationTests
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

    [TestCase("int32 value = 1; value = 42; Ensure(value, 42);", TestName = "Int32 Local Should Accept Reassignment")]
    [TestCase("int32 value = 4; value = value + 3; Ensure(value, 7);", TestName = "Assignment Should Read Old Value Before Replacing It")]
    [TestCase("int32 first = 10; int32 second = first; first = 99; Ensure(second, 10);", TestName = "Earlier Local Copy Should Preserve Original Value")]
    [TestCase("int32 value = 2; value = Double(value); Ensure(value, 4);", TestName = "Assignment Should Accept Value Producing Function Call")]
    [TestCase("bool flag = false; flag = true; Require(flag);", TestName = "Boolean Local Should Accept Reassignment")]
    [TestCase("bool flag = true; flag = not flag; Require(not flag);", TestName = "Boolean Assignment Should Read Current Value")]
    [TestCase("int32 value = 1; if (true) { value = 8; } Ensure(value, 8);", TestName = "Nested Block Should Assign Enclosing Local")]
    [TestCase("int32 value = 1; if (false) { value = Divide(0); } Ensure(value, 1);", TestName = "Untaken Branch Should Not Evaluate Assignment Value")]
    [TestCase("int32 value = 1; value = 2; value = 3; Ensure(value, 3);", TestName = "Successive Assignments Should Use Latest Value")]
    [TestCase("bool flag = true; flag = false and IsPositive(Divide(0)); Require(not flag);", TestName = "Boolean Assignment Should Preserve Short Circuit Evaluation")]
    [TestCase("int32 value = 1; int32 other = 2; other = value; value = 9; Ensure(other, 1);", TestName = "Assignment Should Copy Current Value And Not Alias Storage")]
    public async Task AssignmentShouldExecute(string body)
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

    [TestCase(TestName = "Assignments In Both Dynamic Branches Should Preserve Runtime Values")]
    public async Task ConditionalAssignmentsShouldExecute()
    {
        string functions = Helpers + """

            public int32 Choose(bool condition)
            {
                int32 value = 10;

                if (condition)
                {
                    value = value * 2;
                }
                else
                {
                    value = value + 5;
                }

                return value;
            }

            public int32 ConditionalIncrement(bool condition)
            {
                int32 value = 10;

                if (condition)
                {
                    value = 20;
                }

                return value;
            }

            public int32 Nested(bool outer, bool inner)
            {
                int32 result = 1;

                if (outer)
                {
                    result = 2;

                    if (inner)
                    {
                        result = 3;
                    }
                }
                else if (inner)
                {
                    result = 4;
                }

                return result;
            }

            public bool Toggle(bool flag)
            {
                bool result = flag;
                result = not result;
                return result;
            }

            public int32 ReturnAfterAssignment(bool flag)
            {
                int32 result = 1;

                if (flag)
                {
                    result = 7;
                    return result;
                }

                result = 9;
                return result;
            }
            """;

        string root = CreateRoot("""
            Ensure(Choose(true), 20);
            Ensure(Choose(false), 15);

            Ensure(ConditionalIncrement(true), 20);
            Ensure(ConditionalIncrement(false), 10);

            Ensure(Nested(true, true), 3);
            Ensure(Nested(true, false), 2);
            Ensure(Nested(false, true), 4);
            Ensure(Nested(false, false), 1);

            Require(Toggle(false));
            Require(not Toggle(true));

            Ensure(ReturnAfterAssignment(true), 7);
            Ensure(ReturnAfterAssignment(false), 9);
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

    [TestCase(TestName = "Assignment Should Preserve Source Ordered Call Arguments")]
    public async Task AssignmentResultShouldBeUsableBySubsequentCall()
    {
        string body = """
            int32 value = 1;
            value = Double(value);
            Ensure(value, 2);
            value = Double(value);
            Ensure(value, 4);
            """;

        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeTrue();

        int exitCode = await RunExecutable(result.Executable!.FilePath);

        exitCode
            .Should()
            .Be(0);
    }

    [TestCase("missing = 42;", ErrorType.UnknownIdentifier, TestName = "Unknown Assignment Target Should Be Diagnosed")]
    [TestCase("value = 42; int32 value = 1;", ErrorType.UnknownIdentifier, TestName = "Assignment Before Declaration Should Be Diagnosed")]
    [TestCase("int32 value = 1; value = true;", ErrorType.UnsupportedMVPExpression, TestName = "Int32 Local Should Reject Boolean Assignment")]
    [TestCase("bool value = true; value = 42;", ErrorType.UnsupportedMVPExpression, TestName = "Boolean Local Should Reject Int32 Assignment")]
    [TestCase("int32 value = 1; value = IsPositive(1);", ErrorType.UnsupportedMVPExpression, TestName = "Assignment Should Reject Incorrect Call Result Type")]
    [TestCase("if (true) { int32 inner = 1; } inner = 2;", ErrorType.UnknownIdentifier, TestName = "Branch Local Should Not Be Assignable Outside Its Scope")]
    public async Task InvalidAssignmentShouldProduceDiagnostic(string body, ErrorType expectedError)
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

    [TestCase(TestName = "Function Parameter Assignment Should Remain Unsupported")]
    public async Task ParameterAssignmentShouldBeRejected()
    {
        string functions = Helpers + """

            public int32 Invalid(int32 value)
            {
                value = 42;
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

    [TestCase("int32 value = 1; value = ;", TestName = "Missing Right Hand Side Should Be Diagnosed")]
    [TestCase("int32 value = 1; value = 2", TestName = "Missing Assignment Semicolon Should Be Diagnosed")]
    [TestCase("int32 value = 1; value = value = 2;", TestName = "Assignment Chaining Should Not Be Accepted")]
    [TestCase("int32 value = 1; Ensure(value = 2, 2);", TestName = "Assignment Must Not Produce An Expression Value")]
    public async Task MalformedAssignmentShouldProduceSyntaxDiagnostic(string body)
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
                    name = "Local Assignment"
                    assembly = "LocalAssignment"
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