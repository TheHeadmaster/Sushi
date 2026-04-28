namespace Sushi.Compilation;

/// <summary>
/// Represents a compiled intermediate file that has contents and a path.
/// </summary>
public sealed class CompiledFile
{
    /// <summary>
    /// The path of the file with the name and extension.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The name of the file.
    /// </summary>
    public string FileName => Path.GetFileName(this.FilePath);

    /// <summary>
    /// The file extension.
    /// </summary>
    public string FileExtension => Path.GetExtension(this.FilePath);

    /// <summary>
    /// The content of the file.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
