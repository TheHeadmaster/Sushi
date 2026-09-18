using System.Net;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Sushi.Diagnostics;

namespace Sushi;

/// <summary>
/// Contains the options passed to the compiler through command line arguments.
/// </summary>
public sealed class CompilerOptions
{
    /// <summary>
    /// The IP Address to listen on when in LSP mode.
    /// </summary>
    public IPEndPoint? ListenEndpoint { get; private set; }

    /// <summary>
    /// Determines by which method the Language Server Protocol is transported between server and client.
    /// </summary>
    public LSPTransportMethod LSPTransportMethod { get; private set; }

    /// <summary>
    /// Whether the compiler is running in LSP mode.
    /// </summary>
    public bool IsInLSPMode { get; private set; }

    /// <summary>
    /// Creates a new instance of <see cref="CompilerOptions"/>.
    /// </summary>
    private CompilerOptions() { }

    /// <summary>
    /// Processes the command line arguments into a <see cref="CompilerOptions"/> object.
    /// </summary>
    /// <param name="args">
    /// The command line arguments.
    /// </param>
    /// <param name="levelSwitch">
    /// The level switch used to determine what log level to log with.
    /// </param>
    /// <returns>
    /// A <see cref="CompilerOptions"/> object.
    /// </returns>
    public static CompilerOptions FromCommandLineArguments(string[] args, LoggingLevelSwitch levelSwitch)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(levelSwitch);

        CompilerOptions options = new();

        Dictionary<string, string> arguments = [with(StringComparer.OrdinalIgnoreCase)];

        HashSet<string> flags = [with(StringComparer.OrdinalIgnoreCase)];

        string? key = null;

        foreach (string arg in args)
        {
            if (key is not null)
            {
                arguments[key] = arg;
                key = null;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                key = arg[2..];
                continue;
            }
            else if (arg.StartsWith('-'))
            {
                flags.Add(arg[1..]);
                continue;
            }
            else
            {
                Log.Error("Invalid parameter {Parameter} with no key specified. Please use key value pairs (--project \"C:\\Path\\To\\Folder\") or flags (-debug).", arg);
                Program.Exit(ExitCode.InvalidParameterSyntax);
            }
        }

        if (key is not null)
        {
            Log.Error("Parameter --{Parameter} requires a value.", key);
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        if (arguments.TryGetValue("tcp", out string? endpointString))
        {
            if (!IPEndPoint.TryParse(endpointString, out IPEndPoint? endpoint))
            {
                Log.Error("Invalid TCP endpoint {Endpoint}.", endpointString);
                Program.Exit(ExitCode.InvalidParameterSyntax);
            }

            options.LSPTransportMethod = LSPTransportMethod.TCP;
            options.ListenEndpoint = endpoint;
        }

        if (arguments.ContainsKey("tcp") && flags.Contains("stdio"))
        {
            Log.Error("Cannot use LSP with both --tcp and -stdio parameters. Choose one or the other.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        if (flags.Contains("lsp"))
        {
            options.IsInLSPMode = true;
        }

        if (flags.Contains("stdio"))
        {
            options.LSPTransportMethod = LSPTransportMethod.Stdio;
        }

        if (flags.Contains("debug"))
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
        }

        options.Validate();

        return options;
    }

    /// <summary>
    /// Validates the options generated from the command line arguments.
    /// </summary>
    private void Validate()
    {
        if (!this.IsInLSPMode && this.LSPTransportMethod is not LSPTransportMethod.None)
        {
            Log.Error("Cannot use LSP without supplying a transport method. Either use -stdio or ---tcp ip:port.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        if (this.LSPTransportMethod is LSPTransportMethod.TCP && this.ListenEndpoint is null)
        {
            Log.Error("Cannot use LSP without supplying a transport method. Either use -stdio or ---tcp ip:port.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }
    }
}