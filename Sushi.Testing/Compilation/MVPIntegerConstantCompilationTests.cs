using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

namespace Sushi.Testing.Compilation;

[TestFixture]
public class MVPIntegerConstantCompilationTests
{
    [TestCase("6 * 7", 42, TestName = "Compiler Should Execute Constant Multiplication")]
    [TestCase("(6 + 1) * 3 - 2", 19, TestName = "Compiler Should Execute Nested Arithmetic")]
    [TestCase("7 & 3", 3, TestName = "Compiler Should Execute Integer Bitwise And")]
    [TestCase("10 | 5", 15, TestName = "Compiler Should Execute Integer Bitwise Or")]
    [TestCase("7 ^ 3", 4, TestName = "Compiler Should Execute Integer Bitwise Xor")]
    [TestCase("~(-2)", 1, TestName = "Compiler Should Execute Integer Complement")]
    [TestCase("3 << 2", 12, TestName = "Compiler Should Execute Integer Left Shift")]
    [TestCase("64 >> 2", 16, TestName = "Compiler Should Execute Integer Right Shift")]
    [TestCase("1 | 2 ^ 3 & 1", 3, TestName = "Compiler Should Execute Bitwise Precedence")]
    public async Task CompileShould_0(string expression, int expectedExitCode)
    {
        await WithProject(expression, async compilation =>
        {
            compilation.Diagnostics
                .Where(diagnostic => diagnostic.Severity == Sushi.Diagnostics.DiagnosticSeverity.Error)
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

    [TestCase("1 / 0", ErrorType.IntegerDivisionByZero, TestName = "Compiler Should Reject Constant Division By Zero")]
    [TestCase("2147483647 + 1", ErrorType.IntegerConstantOutOfRange, TestName = "Compiler Should Reject Out Of Range Constant Arithmetic")]
    [TestCase("1 << 32", ErrorType.InvalidIntegerShiftCount, TestName = "Compiler Should Reject Invalid Shift Count")]
    [TestCase("1 << 31", ErrorType.IntegerConstantOutOfRange, TestName = "Compiler Should Reject Left Shift Overflow")]
    [TestCase("2147483648 & 0", ErrorType.IntegerConstantOutOfRange, TestName = "Compiler Should Reject Out Of Range Bitwise Operand")]
    public async Task CompileShould_1(string expression, ErrorType expectedError)
    {
        await WithProject(expression, compilation =>
        {
            compilation.Succeeded
                .Should()
                .BeFalse();

            compilation.Executable
                .Should()
                .BeNull();

            compilation.Diagnostics
                .Should()
                .ContainSingle(diagnostic => diagnostic.IsType(expectedError));

            return Task.CompletedTask;
        });
    }

    private static async Task WithProject(string expression, Func<MVPCompilationResult, Task> verify)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
    
        try
        {
            string source = $$"""
                package Example;
                namespace Example;
    
                public int32 launch()
                {
                    return {{expression}};
                }
                """;
    
            MVPCompilationResult compilation = await LegacyFunctionCompilationHelper.CompileAsync(
                source, Path.Combine(directory, "IntegerExecution"), CancellationToken.None);
    
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