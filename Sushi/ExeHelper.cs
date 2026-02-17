using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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

            Console.WriteLine($"\nProcess exited with code: {process.ExitCode}");
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
            Console.WriteLine(line);
        }
    }
}
