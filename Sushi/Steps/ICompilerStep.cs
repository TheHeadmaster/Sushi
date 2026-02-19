using System.Diagnostics.CodeAnalysis;

namespace Sushi.Steps;

/// <summary>
/// Represents a step of the compiler that handles a specific part of the compilation process.
/// </summary>
public interface ICompilerStep
{
    /// <summary>
    /// The step number. Used to order steps.
    /// </summary>
    int StepNumber { get; }

    /// <summary>
    /// Initializes the <see cref="ICompilerStep"/>.
    /// </summary>
    /// <param name="job">
    /// The job running the step. Use this to pass data back to the job during initialization.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task Initialize([NotNull] CompileJob job);

    /// <summary>
    /// Runs the <see cref="ICompilerStep"/>.
    /// </summary>
    /// <param name="job">
    /// The job running the step. Use this to pass data back to the job during execution.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task Run([NotNull] CompileJob job);
}
