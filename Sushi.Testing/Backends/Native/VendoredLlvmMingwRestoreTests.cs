using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

namespace Sushi.Testing.Backends.Native;

/// <summary>
/// Verifies that a clean checkout can reconstruct LLVM-MinGW solely from its committed chunks.
/// </summary>
[TestFixture]
public sealed class VendoredLlvmMingwRestoreTests
{
    [Test]
    public async Task CleanDestinationShouldReconstructAndRepeatablyRestoreTheCommittedToolchain()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The pinned native toolchain is supported on Windows x64 only.");
        }

        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "tools", "RestoreVendoredLlvmMingw.ps1");
        string destinationRoot = Path.Combine(Path.GetTempPath(), "Sushi Vendored Toolchain 雪", Guid.NewGuid().ToString("N"), "llvm-mingw");

        try
        {
            Directory.Exists(destinationRoot).Should().BeFalse();
            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("Installed verified LLVM-MinGW");
            NativeToolchainResolver.ValidateToolchainRoot(destinationRoot, isOverride: true);

            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("already exists");

            Directory.Delete(destinationRoot, recursive: true);
            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("Installed verified LLVM-MinGW");
            NativeToolchainResolver.ValidateToolchainRoot(destinationRoot, isOverride: true);
        }
        finally
        {
            string parent = Path.GetDirectoryName(destinationRoot)!;
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "RestoreVendoredLlvmMingw.ps1"))
                && File.Exists(Path.Combine(directory.FullName, "tools", "llvm-mingw.json")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Sushi checkout containing the vendored LLVM-MinGW archive.");
    }

    private static async Task<string> RunRestore(string scriptPath, string destinationRoot)
    {
        ProcessStartInfo startInfo = new("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-DestinationRoot");
        startInfo.ArgumentList.Add(destinationRoot);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Windows PowerShell for vendored toolchain reconstruction.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        string output = await standardOutput;
        string error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Vendored toolchain reconstruction failed with exit code {process.ExitCode}: {error}{output}");
        }

        return output;
    }
}
