using System.Diagnostics;
using Serilog;

namespace Sushi;

public static class ExeHelper
{
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

            using Process? process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Process \"{fileName}\" failed to start");

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
            Log.Warning($"An error occurred: {ex.Message}");
        }
    }

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
                    string[] existing = Directory.GetFiles(path, $"{file}.*", SearchOption.AllDirectories);

                    string? bestPath = existing.OrderByDescending(x => x?.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ?? false).FirstOrDefault();

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
