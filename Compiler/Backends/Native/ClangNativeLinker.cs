using System.ComponentModel;
using System.Diagnostics;

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

        string temporaryPath = CreateTemporaryOutputPath(fullOutputPath);

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "clang",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add($"--target={objectArtifact.TargetTriple}");
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
                throw new InvalidOperationException("Clang was not found. The MVP native linker requires clang to be available on PATH.", exception);
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