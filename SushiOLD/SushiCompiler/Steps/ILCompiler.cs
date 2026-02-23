using Serilog;
using System.Diagnostics;

namespace SushiCompiler.Steps;

internal static class ILCompiler
{
    internal static async Task Compile()
    {
        Log.Information("Compiling IL using .NET IL Assembler (ilasm)...");

        string ilasmPath = Directory.GetFiles(Directory.GetCurrentDirectory(), "ilasm.exe", SearchOption.AllDirectories).First();

        using Process process = new();
        process.StartInfo.FileName = ilasmPath;
        process.StartInfo.Arguments = "TestIL.il /output:MyAssembly.dll /dll";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

        process.OutputDataReceived += (sender, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data))
            {
                return;
            }

            if (args.Data.StartsWith("Error:"))
            {
                Log.Error(args.Data.Replace("Error: ", string.Empty));
            }
            else
            {
                Log.Information(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data))
            {
                return;
            }

            Log.Error("An error occurred: {Message}", args.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode is not 0)
            {
                Log.Error("ILASM Exited with code {ExitCode}", process.ExitCode);
                return;
            }

            Log.Information($"IL Compilation complete.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred: {Message}", ex.Message);
        }
    }
}
