namespace Sushi.Backends.Native;

/// <summary>
/// Identifies a native object file produced by the compiler.
/// </summary>
public sealed record NativeObjectArtifact
{
    /// <summary>
    /// Gets the absolute path of the emitted object file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the LLVM target triple for which the object was emitted.
    /// </summary>
    public string TargetTriple { get; }

    /// <summary>
    /// Creates a native object artifact.
    /// </summary>
    /// <param name="filePath">
    /// The path of the emitted object file.
    /// </param>
    /// <param name="targetTriple">
    /// The LLVM target triple represented by the object file.
    /// </param>
    public NativeObjectArtifact(string filePath, string targetTriple)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentException.ThrowIfNullOrEmpty(targetTriple);

        this.FilePath = Path.GetFullPath(filePath);
        this.TargetTriple = targetTriple;
    }
}