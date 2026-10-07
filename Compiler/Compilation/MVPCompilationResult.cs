using Sushi.Backends.Native;
using Sushi.Diagnostics;

namespace Sushi.Compilation;

/// <summary>
/// Represents the result of one end-to-end MVP project compilation.
/// </summary>
public sealed record MVPCompilationResult
{
    /// <summary>
    /// Gets the diagnostics produced while analyzing the project and its source files.
    /// </summary>
    public IReadOnlyList<SushiDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the generated executable when compilation completed successfully.
    /// </summary>
    public NativeExecutableArtifact? Executable { get; }

    /// <summary>
    /// Gets whether compilation produced a native executable.
    /// </summary>
    public bool Succeeded => this.Executable is not null;

    /// <summary>
    /// Creates a compilation result.
    /// </summary>
    /// <param name="diagnostics">
    /// The diagnostics produced during project and source analysis.
    /// </param>
    /// <param name="executable">
    /// The generated executable, or <see langword="null"/> when source diagnostics prevented code generation.
    /// </param>
    public MVPCompilationResult(IReadOnlyList<SushiDiagnostic> diagnostics, NativeExecutableArtifact? executable)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        this.Diagnostics = [.. diagnostics];
        this.Executable = executable;
    }
}