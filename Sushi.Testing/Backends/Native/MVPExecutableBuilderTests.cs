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

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Native Toolchain ü " + Guid.NewGuid().ToString("N"));
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

            await AssertExecutableReturns(executable.FilePath, 42);

            string secondOutputPath = Path.Combine(directory, "Second Output", "Return42Again.exe");
            NativeExecutableArtifact secondExecutable = await new MVPExecutableBuilder().BuildAsync(function, secondOutputPath, CancellationToken.None);
            await AssertExecutableReturns(secondExecutable.FilePath, 42);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "MVP Executable Builder Should Link Void Programs Without Toolchain Runtime Dependencies")]
    public async Task BuildAsyncShouldLinkVoidProgramsWithoutToolchainRuntimeDependencies()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "launch",
            IRType.Void,
            [
                new LoweredBasicBlock("entry", [], new LoweredReturnTerminator())
            ]);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Void Native " + Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(directory, "void.exe");

        try
        {
            NativeExecutableArtifact executable = await new MVPExecutableBuilder().BuildAsync(function, outputPath, CancellationToken.None);
            await AssertExecutableReturns(executable.FilePath, 0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "MVP Executable Builder Should Link Root Activity Lifecycle To Zero")]
    public async Task BuildRootActivityAsyncShouldLinkEntryAndStopLifecycle()
    {
        LoweredFunction entry = CreateVoidFunction("__sushi_root_entry");
        LoweredFunction stop = CreateVoidFunction("__sushi_root_stop");
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Root Native " + Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(directory, "root.exe");

        try
        {
            NativeExecutableArtifact executable = await new MVPExecutableBuilder().BuildRootActivityAsync(entry, stop, outputPath, CancellationToken.None);
            await AssertExecutableReturns(executable.FilePath, 0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static LoweredFunction CreateVoidFunction(string name)
        => new(
            IRAccessibility.Public,
            name,
            IRType.Void,
            [
                new LoweredBasicBlock("entry", [], new LoweredReturnTerminator())
            ]);

    private static async Task AssertExecutableReturns(string executablePath, int expectedExitCode)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows())
        {
            foreach (string variable in new[]
            {
                NativeToolchainResolver.OverrideEnvironmentVariable,
                "INCLUDE",
                "LIB",
                "LIBPATH",
                "WindowsSdkDir",
                "WindowsSDKVersion",
                "UniversalCRTSdkDir",
                "UCRTVersion",
                "VCToolsInstallDir",
                "VCINSTALLDIR",
                "VSINSTALLDIR",
                "VisualStudioVersion"
            })
            {
                startInfo.Environment.Remove(variable);
            }

            startInfo.Environment["PATH"] = Environment.GetFolderPath(Environment.SpecialFolder.System);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start().Should().BeTrue();
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(expectedExitCode);
    }
}