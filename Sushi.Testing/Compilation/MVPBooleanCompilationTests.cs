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