namespace Sushi.Backends.Native;

/// <summary>
/// Identifies a linked native executable produced by the compiler.
/// </summary>
public sealed record NativeExecutableArtifact
{
    /// <summary>
    /// Gets the absolute executable path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the LLVM target triple for which the executable was produced.
    /// </summary>
    public string TargetTriple { get; }

    /// <summary>
    /// Creates a native executable artifact.
    /// </summary>
    /// <param name="filePath">
    /// The executable path.
    /// </param>
    /// <param name="targetTriple">
    /// The target triple represented by the executable.
    /// </param>
    public NativeExecutableArtifact(string filePath, string targetTriple)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(targetTriple);

        this.FilePath = Path.GetFullPath(filePath);
        this.TargetTriple = targetTriple;
    }
}