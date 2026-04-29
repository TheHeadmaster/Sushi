using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Formatting.Compact;
using Sushi.Diagnostics;
using Sushi.LSP;

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
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            await Initialize(args);

            if (AppMeta.Options.LanguageServerMode)
            {
                await Diag.MonitorAsync("LanguageServer", RunLanguageServer);
            }
            else
            {
                await Diag.MonitorAsync("Compilation", Run);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled Exception");
            Environment.Exit((int)ExitCode.UnhandledException);
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
    public static async Task Initialize(string[] args)
    {
        while (!Debugger.IsAttached && AppMeta.IsDebug)
        {
            await Task.Delay(1000);
        }

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
    /// Runs the compiler.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static async Task Run()
    {
        SushiLanguageService service = new();
        await service.CompileJob();
    }

    /// <summary>
    /// Runs the language server.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static async Task RunLanguageServer()
    {
        SushiLanguageService service = new();

        LanguageServer server = await LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .OnInitialize((server, request, token) => Task.Run(async () =>
            {
                await service.InitializeWorkspace([.. request.WorkspaceFolders ?? []]);

                return new InitializeResult
                {
                    ServerInfo = new ServerInfo
                    {
                        Name = "Sushi",
                        Version = AppMeta.GetVersion().ToString()
                    },
                    Capabilities = new ServerCapabilities()
                    {

                    }
                };
            }))
            .WithServices(services => services.AddSingleton(service))
            .WithHandler<WorkspaceFoldersHandler>()
        );

        await server.WaitForExit;
    }

    /// <summary>
    /// Event that fires when the application is exiting. Closes and flushes the event buffer.
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
