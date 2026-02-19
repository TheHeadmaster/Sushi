using System.Collections.ObjectModel;
using Sushi.Extensions;
using Sushi.Lexing.Tokenization;
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
    /// Initializes the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Initialize()
    {
        foreach (ICompilerStep step in this.steps)
        {
            await step.Initialize(this);
        }
    }

    /// <summary>
    /// Runs the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Run()
    {
        foreach (ICompilerStep step in this.steps)
        {
            await step.Run(this);
        }
    }
}
