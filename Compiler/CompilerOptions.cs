using System.Net;
using Sushi.Diagnostics.Exceptions;

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
    public string ProjectOrFolderPath { get; private set; } = string.Empty;

    public bool IsDebugLoggingEnabled { get; private set; }

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
      "tcp",
      "project"
    ];

    /// <summary>
    /// Processes the command line arguments into a <see cref="CompilerOptions"/> object.
    /// </summary>
    /// <param name="args">
    /// The command line arguments.
    /// </param>
    /// <returns>
    /// A <see cref="CompilerOptions"/> object.
    /// </returns>
    public static CompilerOptions FromCommandLineArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

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
                    throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Invalid parameter \"{arg}\".");
                }

                continue;
            }
            else if (arg.StartsWith('-'))
            {
                string flag = arg[1..];

                flags.Add(flag);

                if (!allowedFlags.Contains(flag))
                {
                    throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Invalid parameter \"{arg}\".");
                }

                continue;
            }
            else
            {
                throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Invalid parameter {arg} with no key specified. Please use key value pairs (--project \"C:\\Path\\To\\Folder\") or flags (-debug).");
            }
        }

        if (key is not null)
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Parameter --{key} requires a value.");
        }

        if (arguments.TryGetValue("tcp", out string? endpointString))
        {
            if (!IPEndPoint.TryParse(endpointString, out IPEndPoint? endpoint))
            {
                throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Invalid TCP endpoint {endpointString}.");
            }

            options.LSPTransportMethod = LSPTransportMethod.TCP;
            options.ListenEndpoint = endpoint;
        }

        if (arguments.ContainsKey("tcp") && flags.Contains("stdio"))
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, $"Cannot use LSP with both --tcp and -stdio parameters. Choose one or the other.");
        }

        if (!arguments.TryGetValue("project", out string? projectOrFolderPath) && !flags.Contains("lsp"))
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, "A project file or folder path must be specified when compiling. Use the --project \"Path\\To\\File\\OrFolder.susproj\" parameter.");
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
            options.IsDebugLoggingEnabled = true;
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
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, "Cannot specify an LSP transport without enabling LSP mode with -lsp.");
        }

        if (this.IsInLanguageServerMode && this.LSPTransportMethod is LSPTransportMethod.None)
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, "LSP mode requires a transport method. Use -stdio or --tcp ip:port.");
        }

        if (this.LSPTransportMethod is LSPTransportMethod.TCP && this.ListenEndpoint is null)
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidParameterSyntax, "Cannot use LSP without supplying a transport method. Either use -stdio or --tcp ip:port.");
        }

        bool isFilePath = File.Exists(this.ProjectOrFolderPath);

        bool isFolderPath = Directory.Exists(this.ProjectOrFolderPath);

        if (!this.IsInLanguageServerMode && !isFilePath && !isFolderPath)
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidProjectFileOrFolder, $"File or Folder \"{this.ProjectOrFolderPath}\" does not exist on disk. Check your inputs.");
        }

        if (!this.IsInLanguageServerMode && isFilePath && !IsProjectOrSolutionFile(this.ProjectOrFolderPath))
        {
            throw new CompilerOptionsException(CompilerOptionsError.InvalidProjectFileOrFolder, $"File \"{this.ProjectOrFolderPath}\" is not a valid .susproj or .susln file.");
        }

        if (!this.IsInLanguageServerMode && isFolderPath)
        {
            // No need to prefer .susln over .susproj in this step, because we will check that later downstream.
            string? projectFile = Directory.EnumerateFiles(this.ProjectOrFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(IsProjectOrSolutionFile);

            if (string.IsNullOrWhiteSpace(projectFile))
            {
                throw new CompilerOptionsException(CompilerOptionsError.InvalidProjectFileOrFolder, $"Folder \"{this.ProjectOrFolderPath}\" does not contain a valid .susproj or .susln file. Make sure it is in the top directory and not a sub directory.");
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
    private static bool IsProjectOrSolutionFile(string file)
    {
        string extension = Path.GetExtension(file);

        return extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".susln", StringComparison.OrdinalIgnoreCase);
    }
}