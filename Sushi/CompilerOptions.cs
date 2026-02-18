using Microsoft.Extensions.Options;
using Serilog;

namespace Sushi;

/// <summary>
/// Contains the options passed to the compiler through command line arguments.
/// </summary>
public sealed class CompilerOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="CompilerOptions"/>.
    /// </summary>
    private CompilerOptions() { }

    /// <summary>
    /// The path of the project.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Processes the command line arguments into a <see cref="CompilerOptions"/> object.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>An awaitable <see cref="Task"/> that returns a <see cref="CompilerOptions"/> object.</returns>
    public static async Task<CompilerOptions> FromCommandLineArguments(string[] args)
    {
        CompilerOptions options = new();

        Dictionary<string, string> arguments = [];

        List<string> flags = [];

        string? key = null;

        foreach (string arg in args ?? [])
        {
            if (key is not null)
            {
                arguments[key] = arg;
                key = null;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.InvariantCultureIgnoreCase))
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
                Environment.Exit((int)ExitCode.InvalidParameterSyntax);
            }
        }

        if (arguments.TryGetValue("project", out string? path))
        {
            options.ProjectPath = path;
        }

        await options.Validate();

        return options;
    }

    /// <summary>
    /// Validates the options generated from the command line arguments.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private Task Validate()
    {
        if (string.IsNullOrWhiteSpace(this.ProjectPath))
        {
            Log.Error("A project path was not specified. You must specify a path to compile.");
            Environment.Exit((int)ExitCode.ProjectPathNotSpecified);
        }

        if (!Directory.Exists(this.ProjectPath))
        {
            Log.Error("Project path does not exist! Please specify a valid project path.");
            Environment.Exit((int)ExitCode.ProjectPathNotFound);
        }
        else
        {
            Log.Information("Project path given was \"{Path}\".", this.ProjectPath);
        }

        return Task.CompletedTask;
    }
}
