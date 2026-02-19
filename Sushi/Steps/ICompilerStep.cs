namespace Sushi.Steps;

/// <summary>
/// Represents a step of the compiler that handles a specific part of the compilation process.
/// </summary>
public interface ICompilerStep
{
    /// <summary>
    /// Initializes the <see cref="ICompilerStep"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task Initialize();

    /// <summary>
    /// Runs the <see cref="ICompilerStep"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task Run();
}
