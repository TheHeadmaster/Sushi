using Sushi.Backends.LLVM;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends.Native;

/// <summary>
/// Carries one explicitly selected lowered Sushi function through the temporary executable MVP backend pipeline.
/// </summary>
/// <remarks>
/// Selection of the supplied function as the process root is an MVP compiler convention and does not establish
/// Sushi source-level entry-point semantics.
/// </remarks>
public sealed class MVPExecutableBuilder
{
    private readonly LLVMModuleEmitter moduleEmitter = new();

    /// <summary>
    /// Produces a runnable native executable whose temporary native entry shim invokes the supplied Sushi function.
    /// </summary>
    /// <param name="entryFunction">
    /// The lowered Sushi function explicitly selected as the temporary MVP process root.
    /// </param>
    /// <param name="outputPath">
    /// The executable output path.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel compilation or linking.
    /// </param>
    /// <returns>
    /// The linked native executable artifact.
    /// </returns>
    public async Task<NativeExecutableArtifact> BuildAsync(LoweredFunction entryFunction, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entryFunction);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        cancellationToken.ThrowIfCancellationRequested();

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Sushi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        string objectPath = Path.Combine(temporaryDirectory, OperatingSystem.IsWindows() ? "program.obj" : "program.o");

        try
        {
            using LLVMModuleArtifact moduleArtifact = this.moduleEmitter.Emit("Sushi.MVP", entryFunction, cancellationToken);

            TemporaryMVPEntryShim.Add(moduleArtifact, entryFunction.Name);

            NativeObjectArtifact objectArtifact = LLVMObjectEmitter.EmitForHost(moduleArtifact, objectPath, cancellationToken);

            return await ClangNativeLinker.LinkAsync(objectArtifact, outputPath, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}