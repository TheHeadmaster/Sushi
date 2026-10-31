
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends.Native;

/// <summary>
/// Builds native executables from lowered Sushi functions, including the initial task-root lifecycle.
/// </summary>
public sealed class MVPExecutableBuilder(IModuleEmitter moduleEmitter, IObjectEmitter objectEmitter, IEntryShim entryShim)
{
    /// <summary>
    /// Creates the default LLVM-backed executable pipeline.
    /// </summary>
    internal MVPExecutableBuilder() : this(new LLVMModuleEmitter(), new LLVMObjectEmitter(), new TemporaryMVPEntryShim())
    {
    }

    /// <summary>
    /// Builds a native executable using the legacy single-function MVP entry convention.
    /// </summary>
    /// <param name="entryFunction">
    /// The parameterless function selected by an MVP test or caller.
    /// </param>
    /// <param name="outputPath">
    /// The requested executable path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels compilation and linking.
    /// </param>
    /// <returns>
    /// The linked native executable.
    /// </returns>
    public Task<NativeExecutableArtifact> BuildAsync(LoweredFunction entryFunction, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entryFunction);

        if (entryFunction.Parameters.Count != 0)
        {
            throw new NotSupportedException("The temporary MVP entry function must be parameterless.");
        }

        return this.BuildCoreAsync(
            [entryFunction],
            outputPath,
            artifact => entryShim.Add(artifact, entryFunction.Name, entryFunction.ReturnType),
            cancellationToken);
    }

    /// <summary>
    /// Builds an executable that starts one task root, invokes its entry and optional stop hook,
    /// and returns process status zero after normal termination.
    /// </summary>
    /// <param name="entry">
    /// The compiled, parameterless void root entry method.
    /// </param>
    /// <param name="stop">
    /// The optional compiled, parameterless void stop method.
    /// </param>
    /// <param name="outputPath">
    /// The requested executable path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels compilation and linking.
    /// </param>
    /// <returns>
    /// The linked native executable.
    /// </returns>
    public Task<NativeExecutableArtifact> BuildRootActivityAsync(LoweredFunction entry, LoweredFunction? stop, string outputPath, CancellationToken cancellationToken)
    {
        ValidateRootLifecycleMethod(entry);

        if (stop is not null)
        {
            ValidateRootLifecycleMethod(stop);

            if (entry.Name == stop.Name)
            {
                throw new ArgumentException("Entry and stop methods must have distinct native symbols.", nameof(stop));
            }
        }

        IReadOnlyList<LoweredFunction> functions = stop is null ? [entry] : [entry, stop];

        return this.BuildCoreAsync(
            functions,
            outputPath,
            artifact => RootActivityEntryShim.Add(artifact, entry.Name, stop?.Name),
            cancellationToken);
    }

    private async Task<NativeExecutableArtifact> BuildCoreAsync(IReadOnlyList<LoweredFunction> functions, string outputPath, Action<IModuleArtifact> addNativeEntry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(addNativeEntry);
        cancellationToken.ThrowIfCancellationRequested();

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Sushi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        string objectPath = Path.Combine(temporaryDirectory, OperatingSystem.IsWindows() ? "program.obj" : "program.o");

        try
        {
            using IModuleArtifact moduleArtifact = moduleEmitter.Emit("Sushi.MVP", functions, cancellationToken);

            addNativeEntry(moduleArtifact);

            NativeObjectArtifact objectArtifact = objectEmitter.EmitForHost(moduleArtifact, objectPath, cancellationToken);

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

    private static void ValidateRootLifecycleMethod(LoweredFunction method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (method.ReturnType is not IRType.Void || method.Parameters.Count != 0)
        {
            throw new ArgumentException("Root lifecycle methods must be parameterless and return void.", nameof(method));
        }
    }
}