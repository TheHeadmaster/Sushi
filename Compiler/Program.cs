using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Diagnostics;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;
using System.Globalization;
using Serilog.Events;
using Sushi.Diagnostics.Exceptions;

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
            Initialize(args);

            SushiLanguageService service = new();

            if (AppMeta.Options.IsInLanguageServerMode)
            {
                await Diag.Monitor("LanguageServer", service.RunLanguageServer);
            }
            else
            {
                await Diag.Monitor("Compilation", service.CompileJob);
            }
        }
        catch (CompilerOptionsException exception)
        {
            Log.Error("{Message}", exception.Message);
            Exit(exception.Error switch
            {
                CompilerOptionsError.InvalidParameterSyntax => ExitCode.InvalidParameterSyntax,
                CompilerOptionsError.InvalidProjectFileOrFolder => ExitCode.InvalidProjectFileOrFolder,
                _ => ExitCode.UnhandledException
            });
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
    private static void Initialize(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string logsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

        Directory.CreateDirectory(logsPath);

        LoggingLevelSwitch levelSwitch = new();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(new CompactJsonFormatter(), Path.Combine(logsPath, "info.log"), rollingInterval: RollingInterval.Day)
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

        AppMeta.Options = CompilerOptions.FromCommandLineArguments(args);

        if (AppMeta.Options.IsDebugLoggingEnabled)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
        }
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