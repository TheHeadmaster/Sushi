using System.Collections.ObjectModel;
using Sushi.Extensions;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Nodes;
using Sushi.Steps;

namespace Sushi;

/// <summary>
/// The job that runs the compilation in steps.
/// </summary>
public sealed class CompileJob
{
    /// <summary>
    /// The steps.
    /// </summary>
    private readonly ReadOnlyCollection<ICompilerStep> steps = [.. ReflectionEx.GetLeafSubclasses<ICompilerStep>().OrderBy(step => step.StepNumber)];

    /// <summary>
    /// The token files created during the lexing step.
    /// </summary>
    public List<TokenFile> SourceFiles { get; set; } = [];

    /// <summary>
    /// The syntax tree that represents the entirety of the source code.
    /// </summary>
    public AbstractSyntaxTree SyntaxTree { get; set; } = new();

    /// <summary>
    /// Runs the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Run()
    {
        ReadOnlyCollection<ICompilerStep> filteredSteps = this.steps;

        if (AppMeta.Options.IntermediateOnly)
        {
            filteredSteps = [.. filteredSteps.Where(step => step.GetType() != typeof(ExecutableCompilingStep))];
        }

        foreach (ICompilerStep step in filteredSteps)
        {
            await step.Initialize(this);
            await step.Run(this);
        }
    }
}
