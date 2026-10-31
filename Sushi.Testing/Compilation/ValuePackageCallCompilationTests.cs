using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies value-producing package-function calls through project binding,
/// structured IR, LLVM emission, and native execution.
/// </summary>
[TestFixture]
public class ValuePackageCallCompilationTests
{
    private const string Helpers = """
        package Example;
        namespace Example;

        public int32 Calculate()
        {
            return Add(Double(10), 1);
        }

        public int32 Add(int32 left, int32 right)
        {
            return left + right;
        }

        public int32 Double(int32 value)
        {
            return value * 2;
        }

        public int32 PlusOne(int32 value)
        {
            return Double(value) + 1;
        }

        public int32 Subtract(int32 left, int32 right)
        {
            return left - right;
        }

        public bool IsPositive(int32 value)
        {
            return value > 0;
        }

        public bool IsExpected(int32 value)
        {
            return Calculate() == value;
        }

        public int32 Divide(int32 divisor)
        {
            return 10 / divisor;
        }

        public void Fail(int32 divisor)
        {
            if (10 / divisor > 0)
            {
                return;
            }
        }

        public void Ensure(int32 actual, int32 expected)
        {
            if (actual != expected)
            {
                Fail(0);
            }
        }

        public void Require(bool condition)
        {
            if (not condition)
            {
                Fail(0);
            }
        }
        """;


    [TestCase("Ensure(Calculate(), 21);", TestName = "Nested Return Values Should Reach Native Caller")]
    [TestCase("Ensure(PlusOne(20), 41);", TestName = "Call Results Should Participate In Arithmetic")]
    [TestCase("Ensure(Subtract(right: Double(2), left: 10), 6);", TestName = "Named Calls Should Map Nested Results To Parameters")]
    [TestCase("Require(IsPositive(Calculate()));", TestName = "Boolean Call Results Should Reach Caller")]
    [TestCase("Require(IsExpected(21));", TestName = "Boolean Return Expressions Should Invoke Other Functions")]
    [TestCase("Ensure(Add(Double(3), PlusOne(4)), 15);", TestName = "Multiple Nested Value Calls Should Compose")]
    public async Task ValueCallShould_0(string body)
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

    [TestCase(TestName = "Value Call In Root Condition Should Select True Branch")]
    public async Task ValueCallShould_1()
    {
        string body = """
            if (IsPositive(Calculate()))
            {
                Ensure(21, Calculate());
            }
            else
            {
                Fail(0);
            }
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

    [TestCase(TestName = "Short Circuit Should Skip A Valid But Failing Value Call")]
    public async Task ValueCallShould_2()
    {
        string body = """
            if (false and IsPositive(Divide(0)))
            {
                Fail(0);
            }
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

    [TestCase(TestName = "Executed Value Call Should Preserve Runtime Arithmetic Failure")]
    public async Task ValueCallShould_3()
    {
        using TestProject project = await TestProject.Create(CreateRoot("Require(IsPositive(Divide(0)));"), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeTrue();

        int exitCode = await RunExecutable(result.Executable!.FilePath);

        exitCode
            .Should()
            .NotBe(0);
    }

    [TestCase("Calculate();", TestName = "Value Returning Invocation Cannot Stand Alone")]
    [TestCase("Ensure(Fail(1), 1);", TestName = "Void Invocation Cannot Supply Value Argument")]
    [TestCase("Ensure(IsPositive(1), 1);", TestName = "Boolean Result Cannot Supply Int32 Argument")]
    [TestCase("Require(Calculate());", TestName = "Int32 Result Cannot Supply Boolean Argument")]
    [TestCase("Require(Missing());", TestName = "Unknown Value Function Must Be Diagnosed")]
    public async Task InvalidValueCallShould_0(string body)
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
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Void Call Cannot Be Returned From Int32 Function")]
    public async Task InvalidValueCallShould_1()
    {
        const string invalidHelper = """
            package Example;
            namespace Example;

            public void Log()
            {
                return;
            }

            public int32 Calculate()
            {
                return Log();
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), invalidHelper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Return Type Must Not Resolve Ambiguous Overload")]
    public async Task InvalidValueCallShould_2()
    {
        const string overloaded = """
            package Example;
            namespace Example;

            public int32 Choose(int32 value)
            {
                return value;
            }

            public bool Choose(bool value)
            {
                return value;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Require(Choose(true));"), overloaded);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Unreachable Incorrect Value Invocation Must Still Be Diagnosed")]
    public async Task InvalidValueCallShould_3()
    {
        string body = """
            return;

            if (false)
            {
                Require(Fail(1));
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot(body), Helpers);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase("Trace(); return 42;", TestName = "Value Function Should Permit Invocation Before Return")]
    [TestCase("return 42; Trace();", TestName = "Value Function Should Permit Valid Unreachable Statement After Return")]
    [TestCase("return 42; return 7;", TestName = "Value Function Should Permit Multiple Returns")]
    [TestCase("if (true) { return 42; }", TestName = "Constant True Branch Should Satisfy Return Contract")]
    [TestCase("if (false) { return 7; } else { return 42; }", TestName = "Constant False Branch Should Select Returning Else")]
    public async Task ValueReturnStatementsShould_0(string body)
    {
        string helper = Helpers + $$"""

            public void Trace()
            {
                return;
            }

            public int32 Evaluate()
            {
                {{body}}
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Ensure(Evaluate(), 42);"), helper);
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

    [TestCase("", TestName = "Empty Value Body Should Have Missing Return Path")]
    [TestCase("Trace();", TestName = "Value Body With Only Invocation Should Have Missing Return Path")]
    [TestCase("if (flag) { return 1; }", TestName = "Conditional Return Without Else Should Have Missing Return Path")]
    [TestCase("if (flag) { return 1; } else { Trace(); }", TestName = "Else Branch Without Return Should Have Missing Return Path")]
    [TestCase("if (flag) { if (flag) { return 1; } } else { return 2; }", TestName = "Nested Conditional Should Detect Missing Return Path")]
    [TestCase("if (false) { return 1; }", TestName = "Unreachable Constant True Branch Should Not Satisfy Return Contract")]
    [TestCase("if (true) { Trace(); }", TestName = "Constant True Branch Without Return Should Have Missing Return Path")]
    public async Task MissingReturnPathShould_0(string body)
    {
        string helper = $$"""
            package Example;
            namespace Example;

            public void Trace()
            {
                return;
            }

            public int32 Evaluate(bool flag)
            {
                {{body}}
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), helper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Executable
            .Should()
            .BeNull();

        SushiDiagnostic diagnostic = result.Diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.MissingReturnPath))
            .Which;

        diagnostic.DisplayName
            .Should()
            .Be("SUSE0323");

        diagnostic.Span.Snapshot.Uri.LocalPath
            .Should()
            .EndWith("Helpers.sus");
    }

    [TestCase(TestName = "Invalid Return Value Should Preserve Specific Diagnostic Without Unsupported Body Error")]
    public async Task InvalidReturnValueShould_0()
    {
        const string helper = """
            package Example;
            namespace Example;

            public int32 Calculate()
            {
                return;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), helper);
        MVPCompilationResult result = await project.Compile();

        result.Succeeded
            .Should()
            .BeFalse();

        result.Executable
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle()
            .Which
            .IsType(ErrorType.MissingReturnValue)
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Void Function Should Continue Supporting Invocation Followed By Return")]
    public async Task VoidFunctionShould_0()
    {
        const string helper = """
            package Example;
            namespace Example;

            public void Log()
            {
                return;
            }

            public void Run()
            {
                Log();
                return;
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("Run();"), helper);
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

    [TestCase(TestName = "Dynamic Conditional Returns Should Preserve Execution Paths")]
    public async Task ValueReturnControlFlowShould_0()
    {
        string helper = Helpers + """

            public int32 Choose(bool flag)
            {
                if (flag)
                {
                    return 10;
                }
                else
                {
                    return 20;
                }
            }

            public int32 Fallback(bool flag)
            {
                if (flag)
                {
                    return 30;
                }

                return 40;
            }

            public int32 Nested(bool outer, bool inner)
            {
                if (outer)
                {
                    if (inner)
                    {
                        return 1;
                    }

                    return 2;
                }
                else if (inner)
                {
                    return 3;
                }

                return 4;
            }

            public bool ToBool(bool flag)
            {
                if (flag)
                {
                    return true;
                }

                return false;
            }

            public int32 Ordered(bool flag)
            {
                Ensure(9, 9);

                if (flag)
                {
                    return 50;
                }

                Ensure(8, 8);
                return 60;
            }
            """;

        string root = CreateRoot("""
            Ensure(Choose(true), 10);
            Ensure(Choose(false), 20);

            Ensure(Fallback(true), 30);
            Ensure(Fallback(false), 40);

            Ensure(Nested(true, true), 1);
            Ensure(Nested(true, false), 2);
            Ensure(Nested(false, true), 3);
            Ensure(Nested(false, false), 4);

            Require(ToBool(true));
            Require(not ToBool(false));

            Ensure(Ordered(true), 50);
            Ensure(Ordered(false), 60);
            """);

        using TestProject project = await TestProject.Create(root, helper);
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

    [TestCase(TestName = "Unreachable Invalid Value Call Should Still Be Diagnosed")]
    public async Task UnreachableValueReturnCodeShould_0()
    {
        string helper = Helpers + """

            public int32 Evaluate()
            {
                return 42;

                if (false)
                {
                    return Missing();
                }
            }
            """;

        using TestProject project = await TestProject.Create(CreateRoot("return;"), helper);
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

        result.Diagnostics
            .Should()
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.MissingReturnPath));
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
                    name = "Value Package Call"
                    assembly = "ValuePackageCall"
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