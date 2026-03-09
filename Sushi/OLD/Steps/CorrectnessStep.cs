using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Serilog;
using Sushi.Correctness;
using Sushi.Extensions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Steps;

/// <summary>
/// Handles the compiler checks to determine whether a given codebase is sound (i.e. follows the rules of the language).
/// </summary>
public sealed class CorrectnessStep : ICompilerStep
{
    public int StepNumber => 3;

    public Task Initialize([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Initializing Sushi compile checker...");

        Log.Information("Initialized Sushi compile checker in {Time}.", startTime.TimeSinceAsString());

        return Task.CompletedTask;
    }

    public async Task Run([NotNull] CompileJob job)
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Checking for correctness...");

        CorrectnessVisitor visitor = new();

        await visitor.Visit(job.SyntaxTree);

        List<CompilerError> errors = await CorrectnessVisitor.CollectErrors();

        foreach (CompilerError error in errors)
        {
            await ReportCompilerError(error);
        }

        Log.Information("Correctness check completed in {Time} with {Count} compiler errors.", startTime.TimeSinceAsString(), errors.Count);

        if (errors.Count > 0)
        {
            Program.Exit(ExitCode.ParsingSyntaxError);
        }
    }

    /// <summary>
    /// Reports a compiler error with a visual cue on the exact line number and position that the error occurred.
    /// </summary>
    /// <param name="source">
    /// The source file the error happened in.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static Task ReportCompilerError(CompilerError error)
    {
        Log.Error("{ErrorReason} at {LineNumber} Position {LinePosition}\n{Line}\n{Padding}{Span}", error.ErrorReason,
            error.LineNumber, error.LinePosition, error.CurrentLine, new string(' ', error.LinePosition), new string('~', error.Span));

        return Task.CompletedTask;
    }
}
