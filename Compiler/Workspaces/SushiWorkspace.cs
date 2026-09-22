using System.Diagnostics.CodeAnalysis;
using Sushi.Source;

namespace Sushi.Workspaces;

/// <summary>
/// Represents an open workspace.
/// </summary>
public sealed class SushiWorkspace
{
    /// <summary>
    /// Contains the source documents in the workspace.
    /// </summary>
    private readonly List<SourceDocument> documents = [];

    /// <summary>
    /// Synchronizes access to the workspace document collection.
    /// </summary>
    private readonly object syncRoot = new();

    /// <summary>
    /// Gets a snapshot of the documents currently in the workspace.
    /// </summary>
    public IReadOnlyList<SourceDocument> Documents
    {
        get
        {
            lock (this.syncRoot)
            {
                return [.. this.documents];
            }
        }
    }

    /// <summary>
    /// Attempts to get a source document by <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">
    /// The <see cref="Uri"/> of the source document to fetch.
    /// </param>
    /// <param name="document">
    /// The document, if it exists.
    /// </param>
    /// <returns>
    /// True if the source document was found. False otherwise.
    /// </returns>
    public bool TryGetDocument(Uri uri, [NotNullWhen(true)] out SourceDocument? document)
    {
        ArgumentNullException.ThrowIfNull(uri);

        lock (this.syncRoot)
        {
            document = this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri));

            return document is not null;
        }
    }

    /// <summary>
    /// Gets a source document by <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">
    /// The <see cref="Uri"/> of the source document to fetch.
    /// </param>
    /// <returns>
    /// The source document.
    /// </returns>
    public SourceDocument GetDocument(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        lock (this.syncRoot)
        {
            SourceDocument? document = this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri));

            return document ?? throw new KeyNotFoundException($"Document \"{uri}\" does not exist in the workspace.");
        }
    }

    /// <summary>
    /// Adds a source document to the workspace.
    /// </summary>
    /// <param name="uri">
    /// The <see cref="Uri"/> of the new document.
    /// </param>
    /// <param name="diskSnapshot">
    /// The source snapshot.
    /// </param>
    /// <returns>
    /// The new source document that was added.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the document already exists.
    /// </exception>
    public SourceDocument AddDocument(Uri uri, SourceSnapshot diskSnapshot)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(diskSnapshot);

        if (!uri.IsSamePath(diskSnapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.", nameof(diskSnapshot));
        }

        lock (this.syncRoot)
        {
            if (this.documents.Any(source => source.Uri.IsSamePath(uri)))
            {
                throw new InvalidOperationException($"Document \"{uri}\" already exists.");
            }

            SourceDocument document = new(uri, diskSnapshot);

            this.documents.Add(document);
            
            return document;
        }
    }

    /// <summary>
    /// Gets an existing source document or atomically adds it if it does not already exist.
    /// </summary>
    /// <param name="uri">
    /// The <see cref="Uri"/> of the source document.
    /// </param>
    /// <param name="diskSnapshot">
    /// The source snapshot.
    /// </param>
    /// <returns>
    /// The new or existing source document.
    /// </returns>
    public SourceDocument GetorAddDocument(Uri uri, SourceSnapshot diskSnapshot)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(diskSnapshot);

        if (!uri.IsSamePath(diskSnapshot.Uri))
        {
            throw new ArgumentException("Snapshot belongs to a different document.", nameof(diskSnapshot));
        }

        lock (this.syncRoot)
        {
            SourceDocument? existing = this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri));

            if (existing is not null)
            {
                return existing;
            }

            SourceDocument document = new(uri, diskSnapshot);

            this.documents.Add(document);

            return document;
        }
    }

    /// <summary>
    /// Removes a source document from the workspace.
    /// </summary>
    /// <param name="uri">
    /// The <see cref="Uri"/> of the document to remove.
    /// </param>
    /// <returns>
    /// True if the document was removed. False if it was not there.
    /// </returns>
    public bool RemoveDocument(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        lock (this.syncRoot)
        {
            if (this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri)) is { } document)
            {
                this.documents.Remove(document);
                return true;
            }

            return false;
        }
    }
}