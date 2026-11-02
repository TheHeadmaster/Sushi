using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Selects a package function within authoritative UTF-8 source, optionally overriding other project files.
/// </summary>
public sealed class CompilerInspectionRequest
{
    /// <summary>
    /// Gets the project file or directory to resolve using ordinary project discovery.
    /// </summary>
    public string ProjectOrFolderPath { get; }

    /// <summary>
    /// Gets the selected source. Its file URI identifies the project source path.
    /// </summary>
    public SourceSnapshot Snapshot { get; }

    /// <summary>
    /// Gets the selection position in canonical UTF-8 bytes, not UTF-16 characters.
    /// </summary>
    public int ByteOffset { get; }

    /// <summary>
    /// Gets the requested compiler representation.
    /// </summary>
    public InspectionStage Stage { get; }

    /// <summary>
    /// Gets other authoritative snapshots, each identifying a different discovered project source file.
    /// </summary>
    public IReadOnlyList<SourceSnapshot> AdditionalSnapshots { get; }

    /// <summary>
    /// Creates a request without reading or writing source files.
    /// </summary>
    /// <param name="projectOrFolderPath">
    /// A .susproj path or directory containing one project.
    /// </param>
    /// <param name="snapshot">
    /// The selected file's authoritative source, including unsaved edits.
    /// </param>
    /// <param name="byteOffset">
    /// A UTF-8 byte position within the selected function declaration or body.
    /// </param>
    /// <param name="stage">
    /// Structured IR or Lowered IR.
    /// </param>
    /// <param name="additionalSnapshots">
    /// Optional overrides for other project source files. Duplicate file identities are rejected.
    /// </param>
    public CompilerInspectionRequest(string projectOrFolderPath, SourceSnapshot snapshot, int byteOffset, InspectionStage stage,
        IReadOnlyList<SourceSnapshot>? additionalSnapshots = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectOrFolderPath);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (byteOffset < 0 || byteOffset > snapshot.SourceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        if (stage is not (InspectionStage.StructuredIR or InspectionStage.LoweredIR))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        SourceSnapshot[] snapshots = additionalSnapshots is null ? [] : [.. additionalSnapshots];
        if (snapshots.Any(item => item is null))
        {
            throw new ArgumentException("Additional snapshots must not contain null entries.", nameof(additionalSnapshots));
        }

        this.ProjectOrFolderPath = Path.GetFullPath(projectOrFolderPath);
        this.Snapshot = snapshot;
        this.ByteOffset = byteOffset;
        this.Stage = stage;
        this.AdditionalSnapshots = Array.AsReadOnly(snapshots);
    }
}