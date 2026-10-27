using Sushi.Backends.Native;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends;

/// <summary>
/// Emits verified modules as native object files for the compiler host.
/// </summary>
public interface IObjectEmitter
{
    /// <summary>
    /// Emits a module as a host-native object file.
    /// </summary>
    /// <param name="artifact">
    /// The owned module to emit.
    /// </param>
    /// <param name="outputPath">
    /// The destination object-file path.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel object emission.
    /// </param>
    /// <returns>
    /// The emitted native object artifact.
    /// </returns>
    public NativeObjectArtifact EmitForHost(IModuleArtifact artifact, string outputPath, CancellationToken cancellationToken);
}