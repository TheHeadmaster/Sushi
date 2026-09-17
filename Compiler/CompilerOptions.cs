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
    public IPAddress ListenAddress { get; set; } = IPAddress.None;

    /// <summary>
    /// Whether the compiler is running in LSP mode.
    /// </summary>
    public bool IsInLSPMode { get; set; }

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

        Dictionary<string, string> arguments = new(StringComparer.OrdinalIgnoreCase);

        HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

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

        if (arguments.TryGetValue("tcp", out string? ipAddress))
        {
            if (!IPAddress.TryParse(ipAddress, out IPAddress? address))
            {
                Log.Error("Invalid IP Address {IPAddress}.", ipAddress);
                Program.Exit(ExitCode.InvalidParameterSyntax);
            }

            options.ListenAddress = address;
        }

        if (flags.Contains("lsp"))
        {
            options.IsInLSPMode = true;
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
        // stub for future argument validation.
    }
}