using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Sushi.Diagnostics;
using Sushi.Source;
using Sushi.Workspaces;
using DiagnosticSeverity = Sushi.Diagnostics.DiagnosticSeverity;
using OmniSharpDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Orchestrates updates and changes tracking for the current open workspace.
/// </summary>
public sealed class WorkspaceOrchestrator
{
    /// <summary>
    /// Represents the current open workspace.
    /// </summary>
    private readonly SushiWorkspace workspace = new();

    /// <summary>
    /// The workspace folders from the client. Contains the sushi project files when running in LSP mode.
    /// </summary>
    private readonly List<WorkspaceFolder> folders = [];

    /// <summary>
    /// Initializes the language service with the information about the currently open workspace.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The folders that are a part of the workspace.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Initialize([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders, CancellationToken cancellationToken)
    {
        await this.ReplaceWorkspaceFolders(workspaceFolders);

        cancellationToken.ThrowIfCancellationRequested();

        await UpdateSourceDocuments(cancellationToken);
    }

    /// <summary>
    /// Replaces the current workspace folders with an entirely new set.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The workspace folders to replace the old ones with.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task ReplaceWorkspaceFolders([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders)
    {
        this.folders.Clear();
        this.folders.AddRange(workspaceFolders);
    }

    /// <summary>
    /// Updates the workspace folders with the current changeset from the changed workspace folders.
    /// </summary>
    /// <param name="addedFolders">
    /// A list of folders that were added.
    /// </param>
    /// <param name="removedFolders">
    /// A list of folders that were removed.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task UpdateWorkspaceFolders([NotNull] IEnumerable<WorkspaceFolder> addedFolders, [NotNull] IEnumerable<WorkspaceFolder> removedFolders, CancellationToken cancellationToken)
    {
        //TODO: TokenFiles will be replaced with SourceDocument structure, do something here that's equivalent
        //this.tokenFiles.Clear();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (WorkspaceFolder addedFolder in addedFolders)
        {
            this.folders.Add(addedFolder);
        }

        foreach (WorkspaceFolder removedFolder in removedFolders)
        {
            this.folders.Remove(removedFolder);
        }

        cancellationToken.ThrowIfCancellationRequested();

        //TODO: TokenFiles will be replaced with SourceDocument structure, do something here that's equivalent
        //await this.UpdateTokenFiles();

        //this.tree = await this.parser.ParseFiles(this.tokenFiles);

        await this.UpdatePostParsing(languageServer);
    }

    /// <summary>
    /// Opens the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="documentUri">
    /// The document uri.
    /// </param>
    /// <param name="version">
    /// The version number of the update request used for concurrency.
    /// </param>
    /// <param name="text">
    /// The text of the document so that it can be changed.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task OpenDocument([NotNull] Uri documentUri, int version, string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = new(documentUri, version, text);

        SourceDocument document = this.workspace.GetOrAddDocument(documentUri, /* disk state */);

        document.UpdateEditor(snapshot);

        await this.AnalyzeDocument(document, snapshot, cancellationToken);
    }


    /// <summary>
    /// Updates the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="documentUri">
    /// The document uri.
    /// </param>
    /// <param name="version">
    /// The version number of the update request used for concurrency.
    /// </param>
    /// <param name="text">
    /// The text of the document so that it can be changed.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task ChangeDocument([NotNull] Uri documentUri, int version, string text, CancellationToken cancellationToken)
    {
        SourceSnapshot snapshot = new(documentUri, version, text);

        SourceDocument document = this.workspace.GetDocument(documentUri);

        document.UpdateEditor(snapshot);
        document.Close();

        await this.AnalyzeDocument(document, document.CurrentSnapshot, cancellationToken);
    }

    /// <summary>
    /// Closes the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="documentUri">
    /// The document uri.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CloseDocument([NotNull] Uri documentUri, CancellationToken cancellationToken)
    {
        SourceDocument document = this.workspace.GetDocument(documentUri);

        SourceSnapshot diskSnapshot = await this.LoadDiskSnapshot(documentUri, cancellationToken);

        document.UpdateDisk(diskSnapshot);
        document.Close();

        await this.AnalyzeDocument(document, document.CurrentSnapshot, cancellationToken);
    }

    /// <summary>
    /// Saves the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="documentUri">
    /// The document uri.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task SaveDocument([NotNull] Uri documentUri, CancellationToken cancellationToken)
    {
        SourceDocument document = this.workspace.GetDocument(documentUri);

        SourceSnapshot diskSnapshot = await this.LoadDiskSnapshot(documentUri, cancellationToken);

        document.UpdateDisk(diskSnapshot);

        await this.AnalyzeDocument(document, document.CurrentSnapshot, cancellationToken);
    }

    /// <summary>
    /// Publishes diagnostic messages for all documents in the workspace.
    /// </summary>
    /// <param name="languageServer">
    /// The language server facade used to make the publish call.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task PublishDiagnosticsForAllDocuments([NotNull] ILanguageServerFacade languageServer)
    {
        List<SushiDiagnostic> diagnostics = await this.GetMessages();

        List<DocumentUri> uris = [.. this.tokenFiles.Select(x => DocumentUri.FromFileSystemPath(x.FilePath))];

        foreach (IGrouping<string, SushiDiagnostic> group in diagnostics.GroupBy(x => x.FilePath))
        {
            int existingIndex = uris.FindIndex(x => x.ToUri().IsSamePath(group.Key));

            if (existingIndex != -1)
            {
                uris.RemoveAt(existingIndex);
            }

            DocumentUri document = DocumentUri.FromFileSystemPath(group.Key);
            await PublishDiagnosticsForDocument(languageServer, version, [.. group], document);
        }

        foreach (DocumentUri uri in uris)
        {
            await PublishDiagnosticsForDocument(languageServer, version, [], uri);
        }
    }

    private static OmniSharpDiagnosticSeverity ToLspSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => OmniSharpDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => OmniSharpDiagnosticSeverity.Warning,
            DiagnosticSeverity.Information => OmniSharpDiagnosticSeverity.Information,
            DiagnosticSeverity.Hint => OmniSharpDiagnosticSeverity.Hint,
            _ => throw new ArgumentOutOfRangeException(nameof(severity));
        };
    }
}