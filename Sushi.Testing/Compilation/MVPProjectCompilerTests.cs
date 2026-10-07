using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;

namespace Sushi.Testing.Compilation;

[TestFixture]
public class MVPProjectCompilerTests
{
    [TestCase(TestName = "MVP Project Compiler Should Compile Sushi Source Into Runnable Native Executable")]
    public async Task CompileAsyncShould_0()
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
                name = "Return 42"
                assembly = "Return42"
                language-version = "1.0"

                [build]
                default = "debug"
                sources = ["Build/**/*.sus"]

                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """,
                CancellationToken.None);

            await File.WriteAllTextAsync(
                Path.Combine(buildDirectory, "Program.sus"),
                """
                package Example;
                namespace Example;

                public int32 launch() {
                    return 42;
                }
                """,
                CancellationToken.None);

            MVPCompilationResult compilation = await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);

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
                .Be(42);
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