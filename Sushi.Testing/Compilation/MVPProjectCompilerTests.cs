using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Compilation;
using Sushi.Diagnostics;

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

    [TestCase(TestName = "MVP Project Compiler Should Return Project Diagnostics Without Attempting Code Generation")]
    public async Task CompileAsyncShould_1()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string projectPath = Path.Combine(directory, ".susproj");
    
        Directory.CreateDirectory(directory);
    
        try
        {
            await File.WriteAllTextAsync(
                projectPath,
                """
                name = 42
                assembly = "Return42"
                language-version = "1.0"
    
                [build]
                default = "debug"
                sources = ["Build/**/*.sus"]
    
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """,
                CancellationToken.None);
    
            MVPCompilationResult compilation = await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);
    
            compilation.Succeeded
                .Should()
                .BeFalse();
    
            compilation.Executable
                .Should()
                .BeNull();
    
            compilation.Diagnostics
                .Should()
                .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.TomlInvalidValueType));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "MVP Project Compiler Should Aggregate Diagnostics Across Source Files")]
    public async Task CompileAsyncShould_2()
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
                name = "Invalid Sources"
                assembly = "InvalidSources"
                language-version = "1.0"
    
                [build]
                default = "debug"
                sources = ["Build/**/*.sus"]
    
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """,
                CancellationToken.None);
    
            await File.WriteAllTextAsync(
                Path.Combine(buildDirectory, "MissingPackage.sus"),
                """
                namespace Example;
    
                public int32 first() {
                    return 1;
                }
                """,
                CancellationToken.None);
    
            await File.WriteAllTextAsync(
                Path.Combine(buildDirectory, "MissingNamespace.sus"),
                """
                package Example;
    
                public int32 second() {
                    return 2;
                }
                """,
                CancellationToken.None);
    
            MVPCompilationResult compilation = await new MVPProjectCompiler().CompileAsync(projectPath, CancellationToken.None);
    
            compilation.Succeeded
                .Should()
                .BeFalse();
    
            compilation.Executable
                .Should()
                .BeNull();
    
            compilation.Diagnostics
                .Should()
                .HaveCount(2);
    
            compilation.Diagnostics
                .Count(diagnostic => diagnostic.IsType(ErrorType.MissingPackageDeclaration))
                .Should()
                .Be(1);
    
            compilation.Diagnostics
                .Count(diagnostic => diagnostic.IsType(ErrorType.MissingNamespaceDeclaration))
                .Should()
                .Be(1);
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