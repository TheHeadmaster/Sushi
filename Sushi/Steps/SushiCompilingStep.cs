using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Serilog;
using Sushi.Compilation;
using Sushi.Extensions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Steps;

/// <summary>
/// Handles the compilation of a syntax tree into assembly source files.
/// </summary>
public sealed class SushiCompilingStep : ICompilerStep
{
    /// <inheritdoc />
    public int StepNumber => 3;

    /// <inheritdoc />
    public async Task Initialize([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Initializing Sushi Compiler...");

        DirectoryInfo intermediateFolder = new(Path.Combine(AppMeta.Options.ProjectPath, "intermediate"));

        if (Directory.Exists(intermediateFolder.FullName))
        {
            Directory.Delete(intermediateFolder.FullName, true);
        }

        Directory.CreateDirectory(intermediateFolder.FullName);

        Log.Information("Initialized Sushi Compiler in {Time}.", startTime.TimeSinceAsString());
    }

    /// <inheritdoc />
    public async Task Run([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Compiling...");

        SushiVisitor visitor = new();

        await visitor.Visit(job.SyntaxTree);

        Log.Information("Compilation completed in {Time}.", startTime.TimeSinceAsString());
    }
}
