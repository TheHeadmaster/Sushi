using Sushi.Intermediate;

namespace Sushi.Backends;

/// <summary>
/// Synthesizes the temporary native process entry shim used by the executable MVP.
/// This does not define Sushi source-level entry-point semantics.
/// </summary>
public interface IEntryShim
{
    /// <summary>
    /// Converts one explicitly selected Sushi function into the target of a generated C-compatible
    /// native entry function. Boolean results are widened to int32 solely for process-exit testing.
    /// </summary>
    /// <param name="artifact">
    /// The module containing the selected Sushi function.
    /// </param>
    /// <param name="selectedFunctionName">
    /// The semantic name of the Sushi function selected by the MVP compiler pipeline.
    /// </param>
    /// <param name="selectedReturnType">
    /// The lowered return type of the selected function.
    /// </param>
    public void Add(IModuleArtifact artifact, string selectedFunctionName, IRType selectedReturnType);
}