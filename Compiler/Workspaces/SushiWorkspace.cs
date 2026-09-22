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

    public IReadOnlyList<SourceDocument> Documents => this.documents.AsReadOnly();

    public bool TryGetDocument(Uri uri, [NotNullWhen(true)] out SourceDocument? document)
    {
        document = this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri));

        return document is not null;
    }

    public SourceDocument GetDocument(Uri uri) => this.documents.First(source => source.Uri.IsSamePath(uri));

    public SourceDocument AddDocument(Uri uri, SourceSnapshot diskSnapshot)
    {
        SourceDocument document = new(uri, diskSnapshot);

        SourceDocument? existing = this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri));

        if (existing is not null)
        {
            throw new InvalidOperationException($"Document \"{uri}\" already exists.");
        }

        this.documents.Add(document);

        return document;
    }

    public bool RemoveDocument(Uri uri)
    {
        if (this.documents.FirstOrDefault(source => source.Uri.IsSamePath(uri)) is { } document)
        {
            this.documents.Remove(document);

            return true;
        }

        return false;
    }
}