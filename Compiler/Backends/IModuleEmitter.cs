using Sushi.Intermediate.Lowering;

namespace Sushi.Backends;

/// <summary>
/// Translates lowered Sushi functions into verified backend IR, such as LLVM.
/// </summary>
public interface IModuleEmitter
{
    /// <summary>
    /// Emits one lowered Sushi function into a new verified module artifact.
    /// </summary>
    /// <param name="moduleName">
    /// The diagnostic identity assigned to the generated module.
    /// </param>
    /// <param name="function">
    /// The lowered function to translate.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel translation.
    /// </param>
    /// <returns>
    /// An owned <see cref="IModuleArtifact"/>. The caller is responsible for disposing it.
    /// </returns>
    public IModuleArtifact Emit(string moduleName, LoweredFunction function, CancellationToken cancellationToken);
}