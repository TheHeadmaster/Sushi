using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Backends.Native;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Backends.Native;

[TestFixture]
public class MVPExecutableBuilderTests
{
    [TestCase(TestName = "MVP Executable Builder Should Produce Runnable Program Returning 42")]
    public async Task BuildAsyncShould_0()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "launch",
            IRType.Int32,
            [
                new LoweredBasicBlock(
                    "entry",
                    [],
                    new LoweredReturnTerminator(new LoweredIntegerConstant(42)))
            ]);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string requestedExecutablePath = Path.Combine(directory, "Return42");

        try
        {
            NativeExecutableArtifact executable = await new MVPExecutableBuilder().BuildAsync(function, requestedExecutablePath, CancellationToken.None);

            File.Exists(executable.FilePath)
                .Should()
                .BeTrue();

            new FileInfo(executable.FilePath).Length
                .Should()
                .BeGreaterThan(0);

            executable.TargetTriple
                .Should()
                .NotBeNullOrWhiteSpace();

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable.FilePath,
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