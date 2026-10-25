using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

[TestFixture]
public class MVPBooleanCompilationTests
{
    [TestCase("true", 1, TestName = "Compiler Should Execute True Returning Function")]
    [TestCase("false", 0, TestName = "Compiler Should Execute False Returning Function")]
    [TestCase("not true", 0, TestName = "Compiler Should Execute Negated True")]
    [TestCase("not false", 1, TestName = "Compiler Should Execute Negated False")]
    [TestCase("not not true", 1, TestName = "Compiler Should Execute Double Negation")]
    [TestCase("not (not false)", 0, TestName = "Compiler Should Execute Parenthesized Negation")]
    [TestCase("1 == 1", 1, TestName = "Compiler Should Execute Integer Equality")]
    [TestCase("1 != 1", 0, TestName = "Compiler Should Execute Integer Inequality")]
    [TestCase("1 < 2", 1, TestName = "Compiler Should Execute Integer Less Than")]
    [TestCase("2 <= 1", 0, TestName = "Compiler Should Execute Integer Less Than Or Equal")]
    [TestCase("3 > 2", 1, TestName = "Compiler Should Execute Integer Greater Than")]
    [TestCase("2 >= 3", 0, TestName = "Compiler Should Execute Integer Greater Than Or Equal")]
    [TestCase("(5 << 2) > (3 * 6)", 1, TestName = "Compiler Should Execute Mixed Integer Comparison")]
    [TestCase("2147483648 > 2147483647", 1, TestName = "Compiler Should Execute Comparison Beyond Int32 Range")]
    [TestCase("(1 << 100) > 0", 1, TestName = "Compiler Should Execute Exact Untyped Shift Comparison")]
    [TestCase("not (1 < 2)", 0, TestName = "Compiler Should Execute Negated Integer Comparison")]
    public async Task CompileShould_0(string expression, int expectedExitCode)
    {
        await WithProject("bool", expression, async compilation =>
        {
            compilation.Diagnostics
                .Should()
                .BeEmpty();

            compilation.Succeeded
                .Should()
                .BeTrue();

            compilation.Executable
                .Should()
                .NotBeNull();

            File.Exists(compilation.Executable!.FilePath)
                .Should()
                .BeTrue();

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = compilation.Executable.FilePath,
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
                .Be(expectedExitCode);
        });
    }

    [TestCase("bool", "1", TestName = "Compiler Should Reject Integer Result From Boolean Function")]
    [TestCase("int32", "true", TestName = "Compiler Should Reject Boolean Result From Integer Function")]
    [TestCase("bool", "true and false", TestName = "Compiler Should Reject Unsupported Logical Expression")]
    [TestCase("bool", "true == false", TestName = "Compiler Should Retain Unsupported Boolean Equality Boundary")]
    [TestCase("int32", "1 < 2", TestName = "Compiler Should Reject Boolean Comparison Result In Integer Function")]
    public async Task CompileShould_1(string returnType, string expression)
    {
        await WithProject(returnType, expression, compilation =>
        {
            compilation.Succeeded
                .Should()
                .BeFalse();

            compilation.Executable
                .Should()
                .BeNull();

            compilation.Diagnostics
                .Should()
                .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));

            return Task.CompletedTask;
        });
    }
    
    [TestCase("1 / 0 > 0", ErrorType.IntegerDivisionByZero, TestName = "Compiler Should Diagnose Division By Zero In Comparison")]
    [TestCase("1 << -1 > 0", ErrorType.InvalidIntegerShiftCount, TestName = "Compiler Should Diagnose Negative Shift Count In Comparison")]
    public async Task CompileShould_2(string expression, ErrorType expectedError)
    {
        await WithProject("bool", expression, compilation =>
        {
            compilation.Succeeded
                .Should()
                .BeFalse();
    
            compilation.Executable
                .Should()
                .BeNull();
    
            SushiDiagnostic diagnostic = compilation.Diagnostics
                .Should()
                .ContainSingle()
                .Which;
    
            diagnostic.IsType(expectedError)
                .Should()
                .BeTrue();
    
            return Task.CompletedTask;
        });
    }

    private static async Task WithProject(string returnType, string expression, Func<MVPCompilationResult, Task> verify)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string buildDirectory = Path.Combine(directory, "Build");
        string projectPath = Path.Combine(directory, ".susproj");

        Directory.CreateDirectory(buildDirectory);

        try
        {
            await File.WriteAllTextAsync(
                projectPath,
                """
                name = "Boolean Execution"
                assembly = "BooleanExecution"
                language-version = "1.0"

                [build]
                default = "debug"
                sources = ["Build/**/*.sus"]

                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """,
                CancellationToken.None);

            string source = $$"""
                package Example;
                namespace Example;

                public {{returnType}} launch() {
                    return {{expression}};
                }
                """;

            await File.WriteAllTextAsync(Path.Combine(buildDirectory, "Program.sus"), source, CancellationToken.None);

            MVPCompilationResult compilation = await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);

            await verify(compilation);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}