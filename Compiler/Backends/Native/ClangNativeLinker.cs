using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sushi.Backends.Native;

/// <summary>
/// Links native Sushi object files into host executables through the Clang compiler driver.
/// </summary>
public sealed class ClangNativeLinker
{
    /// <summary>
    /// Links one native object artifact into an executable.
    /// </summary>
    /// <param name="objectArtifact">
    /// The native object file to link.
    /// </param>
    /// <param name="outputPath">
    /// The requested executable path.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel linking.
    /// </param>
    /// <returns>
    /// The linked executable artifact.
    /// </returns>
    public static async Task<NativeExecutableArtifact> LinkAsync(NativeObjectArtifact objectArtifact, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectArtifact);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(objectArtifact.FilePath))
        {
            throw new FileNotFoundException("The native object file to link does not exist.", objectArtifact.FilePath);
        }

        string fullOutputPath = NormalizeExecutablePath(outputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        NativeTargetConfiguration.ValidateArtifactTarget(
            objectArtifact.TargetTriple,
            OperatingSystem.IsWindows(),
            RuntimeInformation.ProcessArchitecture);

        ResolvedNativeToolchain toolchain = NativeToolchainResolver.ResolveForCurrentHost();
        string temporaryPath = CreateTemporaryOutputPath(fullOutputPath);

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = toolchain.ExecutablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add($"--target={objectArtifact.TargetTriple}");
            if (toolchain.IsBundled)
            {
                ConfigureBundledToolchainEnvironment(startInfo, toolchain);
                startInfo.ArgumentList.Add("-fuse-ld=lld");
            }

            startInfo.ArgumentList.Add(objectArtifact.FilePath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(temporaryPath);

            using Process process = new() { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Clang could not be started.");
                }
            }
            catch (Win32Exception exception)
            {
                string message = toolchain.IsBundled
                    ? $"The bundled Clang executable at {toolchain.ExecutablePath} could not be started. Verify the LLVM-MinGW extraction and Windows execution policy."
                    : "Clang was not found on PATH. The native linker requires clang on non-Windows hosts.";
                throw new InvalidOperationException(message, exception);
            }

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                await standardOutput;
                await standardError;

                throw;
            }

            string output = await standardOutput;
            string error = await standardError;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(CreateLinkFailureMessage(process.ExitCode, output, error));
            }

            if (!File.Exists(temporaryPath))
            {
                throw new InvalidOperationException("Clang reported successful linking but did not produce the requested executable.");
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);

            return new NativeExecutableArtifact(fullOutputPath, objectArtifact.TargetTriple);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ConfigureBundledToolchainEnvironment(ProcessStartInfo startInfo, ResolvedNativeToolchain toolchain)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(toolchain);

        foreach (string variable in new[]
        {
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
            "VisualStudioVersion",
            "CL",
            "_CL_",
            "LIBRARY_PATH",
            "COMPILER_PATH",
            "C_INCLUDE_PATH",
            "CPLUS_INCLUDE_PATH",
            "SDKROOT",
            "CFLAGS",
            "LDFLAGS",
            "CCC_OVERRIDE_OPTIONS"
        })
        {
            startInfo.Environment.Remove(variable);
        }

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string[] pathEntries =
        [
            Path.Combine(toolchain.RootPath!, "bin"),
            systemDirectory,
            windowsDirectory
        ];

        startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, pathEntries.Where(Directory.Exists));
    }

    private static string NormalizeExecutablePath(string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);

        if (OperatingSystem.IsWindows() && string.IsNullOrEmpty(Path.GetExtension(fullPath)))
        {
            return $"{fullPath}.exe";
        }

        return fullPath;
    }

    private static string CreateTemporaryOutputPath(string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        string fileName = Path.GetFileNameWithoutExtension(outputPath);
        string extension = Path.GetExtension(outputPath);

        return Path.Combine(directory ?? string.Empty, $"{fileName}.{Guid.NewGuid():N}{extension}");
    }

    private static string CreateLinkFailureMessage(int exitCode, string standardOutput, string standardError)
    {
        string details = string.Join(
            Environment.NewLine,
            new[] { standardError.Trim(), standardOutput.Trim() }.Where(value => value.Length > 0));

        return details.Length == 0
            ? $"Clang linking failed with exit code {exitCode}."
            : $"Clang linking failed with exit code {exitCode}:{Environment.NewLine}{details}";
    }
}