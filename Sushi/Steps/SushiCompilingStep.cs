using System.Diagnostics.CodeAnalysis;
using Serilog;
using Sushi.Compilation;
using Sushi.Extensions;

namespace Sushi.Steps;

/// <summary>
/// Handles the compilation of a syntax tree into C source files.
/// </summary>
public sealed class SushiCompilingStep : ICompilerStep
{
    /// <inheritdoc />
    public int StepNumber => 4;

    /// <inheritdoc />
    public async Task Initialize([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Initializing Sushi intermediate compiler...");

        DirectoryInfo intermediateFolder = new(Path.Combine(AppMeta.Options.ProjectPath, "intermediate"));

        if (Directory.Exists(intermediateFolder.FullName))
        {
            Directory.Delete(intermediateFolder.FullName, true);
        }

        Directory.CreateDirectory(intermediateFolder.FullName);

        Log.Information("Initialized Sushi intermediate compiler in {Time}.", startTime.TimeSinceAsString());
    }

    /// <inheritdoc />
    public async Task Run([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Compiling intermediate...");

        SushiVisitor visitor = new();

        await visitor.Visit(job.SyntaxTree);

        Log.Information("Intermediate compilation completed in {Time}.", startTime.TimeSinceAsString());
    }
}
