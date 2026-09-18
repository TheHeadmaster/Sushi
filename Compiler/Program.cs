using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;
using System.Globalization;
using Serilog.Events;

namespace Sushi;

/// <summary>
/// Contains the application entry point and top-level application lifecycle.
/// </summary>
public static class Program
{
    /// <summary>
    /// The application entry point.
    /// </summary>
    /// <param name="args">
    /// The command line arguments.
    /// </param>
    private static async Task Main(string[] args)
    {
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await Initialize(args);

            await Diag.Monitor("Run", Run);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled Exception");
            Exit(ExitCode.UnhandledException);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Initializes the compiler.
    /// </summary>
    /// <param name="args">
    /// The command line arguments.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task Initialize(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string logsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

        Directory.CreateDirectory(logsPath);

        LoggingLevelSwitch levelSwitch = new();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(new CompactJsonFormatter(), Path.Combine(logsPath, "info.log"), rollingInterval: RollingInterval.Day)
            .WriteTo.Debug(formatProvider: CultureInfo.CurrentCulture)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}",
                theme: AppMeta.ConsoleTheme,
                applyThemeToRedirectedOutput: true,
                formatProvider: CultureInfo.CurrentCulture,
                standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        Log.Information("Welcome to Sushi Version {Version}.", AppMeta.GetVersion());

        Log.Information("This assembly is running in {Mode} mode.", AppMeta.IsDebug ? "DEBUG" : "RELEASE");

        AppDomain.CurrentDomain.ProcessExit += OnExit;

        AppMeta.Options = CompilerOptions.FromCommandLineArguments(args, levelSwitch);
    }

    /// <summary>
    /// Runs the program in LSP or Compiler mode. 
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task Run()
    {
        if (AppMeta.Options.IsInLSPMode)
        {
            await RunLanguageServer();
            return;
        }

        await RunCompiler();
    }

    /// <summary>
    /// Runs the language server.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task RunLanguageServer()
    {
        
    }

    /// <summary>
    /// Runs the compiler.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task RunCompiler()
    {
        
    }

    /// <summary>
    /// Event that fires when the application is exiting.
    /// </summary>
    /// <param name="sender">
    /// The event sender.
    /// </param>
    /// <param name="args">
    /// The event arguments.
    /// </param>
    private static void OnExit(object? sender, EventArgs args) => Log.CloseAndFlush();

    /// <summary>
    /// Exits the program gracefully.
    /// </summary>
    /// <param name="exitCode">
    /// The exit code to use.
    /// </param>
    [DoesNotReturn]
    public static void Exit(ExitCode exitCode) => Environment.Exit((int)exitCode);
}