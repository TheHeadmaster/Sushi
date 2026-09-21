using System.Diagnostics.CodeAnalysis;

namespace Sushi.Source;

/// <summary>
/// Represents a source document, such as a .sus file.
/// </summary>
/// <param name="uri">
/// The document <see cref="Uri"/>.
/// </param>
/// <param name="diskSnapshot">
/// The current disk snapshot for the source document.
/// </param>
public sealed class SourceDocument(Uri uri, SourceSnapshot diskSnapshot)
{
    /// <summary>
    /// The current snapshot for the source document.
    /// </summary>
    public SourceSnapshot CurrentSnapshot => this.EditorSnapshot ?? this.DiskSnapshot;

    /// <summary>
    /// The snapshot of the source directly from disk.
    /// </summary>
    public SourceSnapshot DiskSnapshot { get; private set; } = diskSnapshot;

    /// <summary>
    /// The snapshot of the source as it exists in-memory. Used for open documents with unsaved changes.
    /// </summary>
    public SourceSnapshot? EditorSnapshot { get; private set; }

    /// <summary>
    /// True if the source document is open in the editor. False otherwise.
    /// </summary>
    public bool IsOpen => this.EditorSnapshot is not null;

    /// <summary>
    /// The document <see cref="Uri"/>.
    /// </summary>
    public Uri Uri { get; } = uri;

    /// <summary>
    /// Updates the disk snapshot.
    /// </summary>
    /// <param name="snapshot">
    /// The new snapshot.
    /// </param>
    public void UpdateDisk([NotNull] SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!this.Uri.IsSamePath(snapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.", nameof(snapshot));
        }

        this.DiskSnapshot = snapshot;
    }

    /// <summary>
    /// Updates the editor snapshot.
    /// </summary>
    /// <param name="snapshot">
    /// The new snapshot.
    /// </param>
    public void UpdateEditor([NotNull] SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!this.Uri.IsSamePath(snapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.", nameof(snapshot));
        }

        this.EditorSnapshot = snapshot;
    }

    /// <summary>
    /// Closes the editor snapshot.
    /// </summary>
    public void Close() => this.EditorSnapshot = null;

    /// <summary>
    /// Returns whether the specified snapshot is the same as the current snapshot for this source document.
    /// </summary>
    /// <param name="snapshot">
    /// The snapshot to check.
    /// </param>
    /// <returns>
    /// True if the two snapshots are the same. False otherwise.
    /// </returns>
    public bool IsCurrent(SourceSnapshot snapshot) => ReferenceEquals(this.CurrentSnapshot, snapshot);
}