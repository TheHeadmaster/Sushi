using System.Diagnostics;
using Serilog;

namespace Sushi;

/// <summary>
/// Contains helper methods for running other executables.
/// </summary>
public static class ExeHelper
{
    /// <summary>
    /// Runs an executable file and pipes its output and error streams to the console.
    /// </summary>
    /// <param name="fileName">
    /// The name of the file.
    /// </param>
    /// <param name="arguments">
    /// The command line arguments to pass to the executable.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static async Task RunExecutableAndOutputToConsole(string fileName, string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                WorkingDirectory = AppMeta.Options.ProjectPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);

            if (process is null)
            {
                Log.Error("Process \"{FileName}\" failed to start", Path.GetFileName(fileName));

                Program.Exit(ExitCode.ProcessFailedToStart);
            }

            // Read output and error streams asynchronously to prevent deadlocks
            Task standardOutputTask = ReadStreamAsync(process.StandardOutput);
            Task standardErrorTask = ReadStreamAsync(process.StandardError);

            await Task.WhenAll(standardOutputTask, standardErrorTask);

            // Wait for the process to complete (optional, as stream tasks await completion implicitly)
            await process.WaitForExitAsync();

            Log.Information("\nProcess exited with code: {ExitCode}", process.ExitCode);
        }
        catch (Exception ex)
        {
            Log.Error("An error occurred: {Message}", ex.Message);

            Program.Exit(ExitCode.ProcessFailedDuringExecution);
        }
    }

    /// <summary>
    /// Reads the specified stream asynchronously.
    /// </summary>
    /// <param name="reader">
    /// The stream to read.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task ReadStreamAsync(StreamReader reader)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            Log.Information(line);
        }
    }

    /// <summary>
    /// Expands environment variables and, if unqualified, locates the file in the working directory
    /// or the evironment's path.
    /// </summary>
    /// <param name="file">The name of the file.</param>
    /// <returns>The fully-qualified path to the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file is not found.</exception>
    public static string GetFilePathFromEnvPath(string file)
    {
        file = Environment.ExpandEnvironmentVariables(file);

        if (File.Exists(file))
        {
            return Path.GetFullPath(file);
        }

        if (string.IsNullOrEmpty(Path.GetDirectoryName(file)))
        {
            foreach (string test in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                string path = test.Trim();

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    continue;
                }

                try
                {
                    string[] existingFiles = Directory.GetFiles(path, $"{file}.*", SearchOption.AllDirectories);

                    string? bestPath = existingFiles.OrderByDescending(file => file?.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ?? false).FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(bestPath))
                    {
                        return Path.GetFullPath(bestPath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }
        }

        throw new FileNotFoundException(new FileNotFoundException().Message, file);
    }
}
