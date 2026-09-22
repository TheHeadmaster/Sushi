using System.Diagnostics.CodeAnalysis;
using Sushi.Analysis;

namespace Sushi.Source;

/// <summary>
/// Represents a source document, such as a .sus file.
/// </summary>
public sealed class SourceDocument
{
    /// <summary>
    /// The current snapshot for the source document.
    /// </summary>
    public SourceSnapshot CurrentSnapshot
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.EditorSnapshot ?? this.DiskSnapshot;
            }
        }
    }

    /// <summary>
    /// The snapshot of the source directly from disk.
    /// </summary>
    public SourceSnapshot DiskSnapshot { get; private set; }

    /// <summary>
    /// The snapshot of the source as it exists in-memory. Used for open documents with unsaved changes.
    /// </summary>
    public SourceSnapshot? EditorSnapshot { get; private set; }

    public AnalysisResult? Analysis { get; private set; }

    /// <summary>
    /// True if the source document is open in the editor. False otherwise.
    /// </summary>
    public bool IsOpen => this.EditorSnapshot is not null;

    /// <summary>
    /// The document <see cref="Uri"/>.
    /// </summary>
    public Uri Uri { get; }

    private readonly object syncRoot = new();

    /// <param name="uri">
    /// The document <see cref="Uri"/>.
    /// </param>
    /// <param name="diskSnapshot">
    /// The current disk snapshot for the source document.
    /// </param>
    public SourceDocument([NotNull] Uri uri, [NotNull] SourceSnapshot diskSnapshot)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(diskSnapshot);

        if (!uri.IsSamePath(diskSnapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.", nameof(diskSnapshot));
        }

        this.DiskSnapshot = diskSnapshot;
        this.Uri = uri;
    }

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

        lock (this.syncRoot)
        {        
            this.DiskSnapshot = snapshot;
        }
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

        
        lock (this.syncRoot)
        {
            this.EditorSnapshot = snapshot;
        }
    }

    /// <summary>
    /// Closes the editor snapshot.
    /// </summary>
    public void Close([NotNull] SourceSnapshot diskSnapshot)
    {
        ArgumentNullException.ThrowIfNull(diskSnapshot);

        if (!this.Uri.IsSamePath(diskSnapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.");
        }

        lock (this.syncRoot)
        {  
            this.DiskSnapshot = diskSnapshot;
            this.EditorSnapshot = null;
        }
    }

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
    public bool TryUpdateAnalysis(AnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        lock (this.syncRoot)
        {
            SourceSnapshot currentSnapshot = this.EditorSnapshot ?? this.DiskSnapshot;

            if (!ReferenceEquals(currentSnapshot, analysis.Snapshot))
            {
                return false;
            }

            this.Analysis = analysis;

            return true;
        }
    }
}