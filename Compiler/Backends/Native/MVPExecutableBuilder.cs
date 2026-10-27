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
    private readonly IModuleEmitter moduleEmitter = new LLVMModuleEmitter();

    private readonly IObjectEmitter objectEmitter = new LLVMObjectEmitter();

    private readonly IEntryShim entryShim = new TemporaryMVPEntryShim();

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

        if (entryFunction.Parameters.Count != 0)
        {
            throw new NotSupportedException("The executable MVP requires a parameterless entry function. Parameterized functions may be emitted as LLVM functions but cannot be selected as the temporary process entry.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Sushi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        string objectPath = Path.Combine(temporaryDirectory, OperatingSystem.IsWindows() ? "program.obj" : "program.o");

        try
        {
            using IModuleArtifact moduleArtifact = this.moduleEmitter.Emit("Sushi.MVP", entryFunction, cancellationToken);

            this.entryShim.Add(moduleArtifact, entryFunction.Name, entryFunction.ReturnType);

            NativeObjectArtifact objectArtifact = this.objectEmitter.EmitForHost(moduleArtifact, objectPath, cancellationToken);

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