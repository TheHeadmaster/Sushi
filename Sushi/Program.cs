using System.Text;
using Serilog;
using Serilog.Formatting.Compact;

namespace Sushi;

/// <summary>
/// Contains application metadata and houses the entry point of the application.
/// </summary>
public static class Program
{
    /// <summary>
    /// The application entry point.
    /// </summary>
    /// <param name="args">
    /// The command line arguments.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task Main(string[] args)
    {
        try
        {
            await Initialize(args);

            CompileJob job = new();

            await job.Initialize();

            await job.Run();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled Exception");
            Environment.Exit((int)ExitCode.UnhandledException);
        }
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

        if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Logs")))
        {
            Directory.CreateDirectory("Logs");
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(new CompactJsonFormatter(), Path.Combine(Directory.GetCurrentDirectory(), "Logs", $"info.log"), rollingInterval: RollingInterval.Day)
            .WriteTo.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}", theme: AppMeta.ConsoleTheme, applyThemeToRedirectedOutput: true)
            .CreateLogger();

        Log.Information("Welcome to SushiCompiler Version {Version}.", await AppMeta.GetVersion());

        Log.Warning("This assembly is running in {Mode} mode.", AppMeta.IsDebug ? "DEBUG" : "RELEASE");

        AppDomain.CurrentDomain.ProcessExit += OnExit;

        AppMeta.Options = await CompilerOptions.FromCommandLineArguments(args);
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
    public static void Exit(ExitCode exitCode) => Environment.Exit((int)exitCode);
}
