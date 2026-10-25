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
    public async Task CompileShould_0(string expression, int expectedExitCode)
    {
        await WithProject(expression, async compilation =>
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

    [TestCase("1 / 0", ErrorType.IntegerDivisionByZero, TestName = "Compiler Should Reject Constant Division By Zero")]
    [TestCase("2147483647 + 1", ErrorType.IntegerConstantOutOfRange, TestName = "Compiler Should Reject Out Of Range Constant Arithmetic")]
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
        string buildDirectory = Path.Combine(directory, "Build");
        string projectPath = Path.Combine(directory, ".susproj");

        Directory.CreateDirectory(buildDirectory);

        try
        {
            await File.WriteAllTextAsync(
                projectPath,
                """
                name = "Constant Arithmetic"
                assembly = "ConstantArithmetic"
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

                public int32 launch() {
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