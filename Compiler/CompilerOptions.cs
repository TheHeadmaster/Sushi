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
    public bool IsInLanguageServerMode { get; private set; }

    /// <summary>
    /// The path of the project or folder containing the project.
    /// </summary>
    public string ProjectOrFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new instance of <see cref="CompilerOptions"/>.
    /// </summary>
    private CompilerOptions() { }

    /// <summary>
    /// The list of allowed flags.
    /// </summary>
    private static readonly HashSet<string> allowedFlags =
    [
      with(StringComparer.OrdinalIgnoreCase),
      "lsp",
      "stdio",
      "debug"  
    ];

    /// <summary>
    /// The list of allowed keys.
    /// </summary>
    private static readonly HashSet<string> allowedKeys =
    [
      with(StringComparer.OrdinalIgnoreCase),
      "tcp"
    ];

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

                if (!allowedKeys.Contains(key))
                {
                    Log.Error("Invalid parameter {Parameter}.", arg);
                    Program.Exit(ExitCode.InvalidParameterSyntax);
                }

                continue;
            }
            else if (arg.StartsWith('-'))
            {
                string flag = arg[1..];

                flags.Add(flag);

                if (!allowedFlags.Contains(flag))
                {
                    Log.Error("Invalid parameter {Parameter}.", arg);
                    Program.Exit(ExitCode.InvalidParameterSyntax);
                }

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

        if (!arguments.TryGetValue("project", out string? projectOrFolderPath) && !flags.Contains("lsp"))
        {
            Log.Error("A project file or folder path must be specified when compiling. Use the --project \"Path\\To\\File\\OrFolder.susproj\" parameter.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        options.ProjectOrFolderPath = projectOrFolderPath ?? string.Empty;

        if (flags.Contains("lsp"))
        {
            options.IsInLanguageServerMode = true;
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
        if (!this.IsInLanguageServerMode && this.LSPTransportMethod is not LSPTransportMethod.None)
        {
            Log.Error("Cannot specify an LSP transport without enabling LSP mode with -lsp.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        if (this.IsInLanguageServerMode && this.LSPTransportMethod is LSPTransportMethod.None)
        {
            Log.Error("LSP mode requires a transport method. Use -stdio or --tcp ip:port.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        if (this.LSPTransportMethod is LSPTransportMethod.TCP && this.ListenEndpoint is null)
        {
            Log.Error("Cannot use LSP without supplying a transport method. Either use -stdio or --tcp ip:port.");
            Program.Exit(ExitCode.InvalidParameterSyntax);
        }

        bool isFilePath = File.Exists(this.ProjectOrFolderPath);

        bool isFolderPath = Directory.Exists(this.ProjectOrFolderPath);

        if (!this.IsInLanguageServerMode && !isFilePath && !isFolderPath)
        {
            Log.Error("File or Folder \"{FileOrFolderPath}\" does not exist on disk. Check your inputs.", this.ProjectOrFolderPath);
            Program.Exit(ExitCode.InvalidProjectFileOrFolder);
        }

        if (!this.IsInLanguageServerMode && isFilePath && !IsProjectOrSolutionFile(this.ProjectOrFolderPath))
        {
            Log.Error("File \"{FilePath}\" is not a valid .susproj or .susln file.", this.ProjectOrFolderPath);
            Program.Exit(ExitCode.InvalidProjectFileOrFolder);
        }

        if (!this.IsInLanguageServerMode && isFolderPath)
        {
            // No need to prefer .susln over .susproj in this step, because we will check that later downstream.
            string? projectFile = Directory.EnumerateFiles(this.ProjectOrFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(IsProjectOrSolutionFile);

            if (string.IsNullOrWhiteSpace(projectFile))
            {
                Log.Error("Folder \"{FolderPath}\" does not contain a valid .susproj or .susln file. Make sure it is in the top directory and not a sub directory.");
                Program.Exit(ExitCode.InvalidProjectFileOrFolder);
            }
        }
    }

    /// <summary>
    /// Checks if the file path is a valid project or solution file.
    /// </summary>
    /// <param name="file">
    /// The file path to check.
    /// </param>
    /// <returns>
    /// True if the file path has a .susproj or .susln extension. False otherwise.
    /// </returns>
    private static bool IsProjectOrSolutionFile(string file) => Path.GetExtension(file) is not ".susproj" and not ".susln";
}