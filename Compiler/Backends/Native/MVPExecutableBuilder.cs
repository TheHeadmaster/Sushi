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
    /// Builds an executable containing a synchronous task root without additional package functions.
    /// </summary>
    /// <param name="entry">
    /// The compiled root entry method.
    /// </param>
    /// <param name="stop">
    /// The optional compiled stop method.
    /// </param>
    /// <param name="outputPath">
    /// The requested executable path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels compilation and linking.
    /// </param>
    /// <returns>
    /// The linked executable.
    /// </returns>
    public Task<NativeExecutableArtifact> BuildRootActivityAsync(LoweredFunction entry, LoweredFunction? stop, string outputPath, CancellationToken cancellationToken)
        => this.BuildRootActivityAsync(entry, stop, [], outputPath, cancellationToken);

    /// <summary>
    /// Builds a task-root executable with additional package functions available
    /// for direct invocation by root lifecycle methods.
    /// </summary>
    /// <param name="entry">
    /// The compiled root entry method.
    /// </param>
    /// <param name="stop">
    /// The optional compiled stop method.
    /// </param>
    /// <param name="packageFunctions">
    /// Additional functions included in the same native module.
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
    public Task<NativeExecutableArtifact> BuildRootActivityAsync(
        LoweredFunction entry,
        LoweredFunction? stop,
        IReadOnlyList<LoweredFunction> packageFunctions,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ValidateRootLifecycleMethod(entry);
        ArgumentNullException.ThrowIfNull(packageFunctions);

        if (stop is not null)
        {
            ValidateRootLifecycleMethod(stop);

            if (entry.Name == stop.Name)
            {
                throw new ArgumentException("Entry and stop methods must have distinct native symbols.", nameof(stop));
            }
        }

        List<LoweredFunction> functions = [.. packageFunctions, entry];

        if (stop is not null)
        {
            functions.Add(stop);
        }

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