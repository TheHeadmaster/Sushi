using System.Diagnostics.CodeAnalysis;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using Sushi.Analysis;
using Sushi.Source;
using Sushi.Workspaces;
using DiagnosticSeverity = Sushi.Diagnostics.DiagnosticSeverity;

using OmniSharpDiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Sushi.Diagnostics;

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

    private readonly DocumentAnalyzer analyzer = new();

    private readonly object foldersSyncRoot = new();

    private readonly SemaphoreSlim mutationGate = new(1, 1);

    private sealed record AnalysisRequest(SourceDocument Document, SourceSnapshot Snapshot);

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
        ArgumentNullException.ThrowIfNull(workspaceFolders);

        List<AnalysisRequest> analysisRequests;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            this.ReplaceWorkspaceFolders(workspaceFolders);

            analysisRequests = await this.UpdateSourceDocuments(cancellationToken);
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeDocuments(analysisRequests, cancellationToken);
    }

    public Task Start(ILanguageServerFacade languageServer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(languageServer);

        this.languageServer = languageServer;

        this.PublishDiagnosticsForAllDocuments(cancellationToken);

        return Task.CompletedTask;
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
    private void ReplaceWorkspaceFolders([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders)
    {
        ArgumentNullException.ThrowIfNull(workspaceFolders);

        lock (this.foldersSyncRoot)
        {
            this.folders.Clear();
            this.folders.AddRange(workspaceFolders);
        }
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
        ArgumentNullException.ThrowIfNull(addedFolders);
        ArgumentNullException.ThrowIfNull(removedFolders);

        List<AnalysisRequest> analysisRequests;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            lock (this.foldersSyncRoot)
            {
                foreach (WorkspaceFolder removedFolder in removedFolders)
                {
                    Uri removedUri = removedFolder.Uri.ToUri();

                    this.folders.RemoveAll(folder => folder.Uri.ToUri().IsSamePath(removedUri));
                }

                foreach (WorkspaceFolder addedFolder in addedFolders)
                {
                    Uri addedUri = addedFolder.Uri.ToUri();

                    bool alreadyExists = this.folders.Any(folder => folder.Uri.ToUri().IsSamePath(addedUri));

                    if (!alreadyExists)
                    {
                        this.folders.Add(addedFolder);
                    }
                }
            }

            analysisRequests = await this.UpdateSourceDocuments(cancellationToken);
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeDocuments(analysisRequests, cancellationToken);
    }

    private async Task<List<AnalysisRequest>> UpdateSourceDocuments(CancellationToken cancellationToken)
    {
        WorkspaceFolder[] folders;

        lock (this.foldersSyncRoot)
        {
            folders = [.. this.folders];
        }

        List<Uri> discoveredDocuments = [];
        List<AnalysisRequest> analysisRequests = [];

        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (WorkspaceFolder folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Uri folderUri = folder.Uri.ToUri();

            if (!folderUri.IsFile)
            {
                continue;
            }

            string folderPath = folderUri.LocalPath;

            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsWorkspaceDocument(filePath))
                {
                    continue;
                }

                Uri documentUri = new(Path.GetFullPath(filePath));

                discoveredDocuments.Add(documentUri);

                SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);

                SourceDocument document = this.workspace.GetOrAddDocument(documentUri, diskSnapshot);
                
                document.UpdateDisk(diskSnapshot);

                analysisRequests.Add(new AnalysisRequest(document, document.CurrentSnapshot));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (SourceDocument document in this.workspace.Documents)
        {
            bool stillExists = discoveredDocuments.Any(uri => uri.IsSamePath(document.Uri));

            if (!stillExists && !document.IsOpen)
            {
                this.workspace.RemoveDocument(document.Uri);
            }
        }

        return analysisRequests;
    }

    private async Task AnalyzeDocuments(IEnumerable<AnalysisRequest> requests, CancellationToken cancellationToken)
    {
        foreach (AnalysisRequest request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await this.AnalyzeDocument(request.Document, request.Snapshot, cancellationToken);
        }
    }

    private static bool IsWorkspaceDocument(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        return extension.Equals(".sus", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".susln", StringComparison.OrdinalIgnoreCase);
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
    public async Task OpenDocument([NotNull] Uri documentUri, int? version, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);

        SourceDocument document;
        SourceSnapshot editorSnapshot;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
         
            if (!this.workspace.TryGetDocument(documentUri, out SourceDocument? existing))
            {
                SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);

                document = this.workspace.GetOrAddDocument(documentUri, diskSnapshot);
            }
            else
            {
                document = existing;
            }

            editorSnapshot = new SourceSnapshot(documentUri, version, text);

            document.UpdateEditor(editorSnapshot);   
        }
        finally
        {
            this.mutationGate.Release();
        }

        this.CancelScheduledAnalaysis(document);

        await this.AnalyzeDocument(document, editorSnapshot, cancellationToken);
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
    public async Task ChangeDocument([NotNull] Uri documentUri, int? version, string text, CancellationToken cancellationToken)
    {
        SourceDocument document;
        SourceSnapshot snapshot = new(documentUri, version, text);

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);

            document.UpdateEditor(snapshot);
        }
        finally
        {
            this.mutationGate.Release();
        }

        this.ScheduleAnalysis(document, snapshot, editorAnalysisDebounce);
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
        SourceDocument document;
        SourceSnapshot snapshot;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);

            SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);

            document.Close(diskSnapshot);

            snapshot = document.CurrentSnapshot;
        }
        finally
        {
            this.mutationGate.Release();
        }

        this.CancelScheduledAnalaysis(document);

        await this.AnalyzeDocument(document, snapshot, cancellationToken);
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
        SourceDocument document;
        SourceSnapshot snapshot;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);
            SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);
            
            document.UpdateDisk(diskSnapshot);

            snapshot = document.CurrentSnapshot;
        }
        finally
        {
            this.mutationGate.Release();
        }

        this.CancelScheduledAnalaysis(document);

        await this.AnalyzeDocument(document, snapshot, cancellationToken);
    }

    private static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static async Task<SourceSnapshot> LoadDiskSnapshot(Uri documentUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);

        if (!documentUri.IsFile)
        {
            throw new ArgumentException("Only file URIs can be loaded from disk.", nameof(documentUri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = await File.ReadAllBytesAsync(documentUri.LocalPath, cancellationToken);

        ReadOnlySpan<byte> sourceBytes = bytes;

        // An initial UTF-8 BOM has no semantic or observable effect in Sushi.
        if (sourceBytes.Length >= 3 && sourceBytes[0] == 0xEF && sourceBytes[1] == 0xBB && sourceBytes[2] == 0xBF)
        {
            sourceBytes = sourceBytes[3..];
        }

        string text = strictUtf8.GetString(sourceBytes);

        return new SourceSnapshot(documentUri, version: null, text);
    }

    private async Task AnalyzeDocument(SourceDocument document, SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        AnalysisResult result = await this.analyzer.Analyze(snapshot, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!document.TryUpdateAnalysis(result))
        {
            return;
        }

        this.PublishDiagnostics(result);
    }

    private static OmniSharpDiagnosticSeverity ToLspSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => OmniSharpDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => OmniSharpDiagnosticSeverity.Warning,
            DiagnosticSeverity.Information => OmniSharpDiagnosticSeverity.Information,
            DiagnosticSeverity.Hint => OmniSharpDiagnosticSeverity.Hint,
            _ => throw new ArgumentOutOfRangeException(nameof(severity))
        };
    }
    
    private static readonly TimeSpan editorAnalysisDebounce = TimeSpan.FromMilliseconds(250);

    private readonly object analysisScheduleSyncRoot = new();

    private readonly Dictionary<SourceDocument, CancellationTokenSource> scheduledAnalyses = [];

    private ILanguageServerFacade? languageServer;

    private void ScheduleAnalysis(SourceDocument document, SourceSnapshot snapshot, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(snapshot);

        CancellationTokenSource cancellation = new();

        lock (this.analysisScheduleSyncRoot)
        {
            if (this.scheduledAnalyses.TryGetValue(document, out CancellationTokenSource? previous))
            {
                previous.Cancel();
            }

            this.scheduledAnalyses[document] = cancellation;
        }

        _ = this.RunScheduledAnalysis(document, snapshot, delay, cancellation);
    }

    private async Task RunScheduledAnalysis(SourceDocument document, SourceSnapshot snapshot, TimeSpan delay, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(delay, cancellation.Token);

            await this.AnalyzeDocument(document, snapshot, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Expected when a newer snapshot supersedes this one.
        }
        
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled exception while performing scheduled analysis for {DocumentUri}.", document.Uri);
        }
#pragma warning restore CA1031 // Do not catch general exception types
        finally
        {
            lock (this.analysisScheduleSyncRoot)
            {
                if (this.scheduledAnalyses.TryGetValue(document, out CancellationTokenSource? current) && ReferenceEquals(current, cancellation))
                {
                    this.scheduledAnalyses.Remove(document);
                }
            }

            cancellation.Dispose();
        }
    }

    private void CancelScheduledAnalaysis(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (this.analysisScheduleSyncRoot)
        {
            if (this.scheduledAnalyses.Remove(document, out CancellationTokenSource? cancellation))
            {
                cancellation.Cancel();
            }
        }
    }

    private void PublishDiagnostics(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (this.languageServer is null)
        {
            return;
        }

        SourceSnapshot snapshot = result.Snapshot;

        Diagnostic[] diagnostics = [.. result.Diagnostics.Select(diagnostic => ToLSPDiagnostic(diagnostic, snapshot))];

        this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.FromFileSystemPath(snapshot.Uri.LocalPath),
            Version = snapshot.Version,
            Diagnostics = new Container<Diagnostic>(diagnostics)
        });
    }

    private static Diagnostic ToLSPDiagnostic(SushiDiagnostic diagnostic, SourceSnapshot expectedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        ArgumentNullException.ThrowIfNull(expectedSnapshot);

        SourceSpan span = diagnostic.Span;

        if (!ReferenceEquals(span.Snapshot, expectedSnapshot))
        {
            throw new InvalidOperationException("Diagnostic belongs to a difference source snapshot.");
        }

        (int startLine, int startCharacter) = ToLSPPosition(span.Snapshot, span.Start);
        (int endLine, int endCharacter) = ToLSPPosition(span.Snapshot, span.End);

        return new Diagnostic
        {
            Code = diagnostic.Code,
            Message = diagnostic.Message,
            Severity = ToLspSeverity(diagnostic.Severity),
            Range = new LSPRange(startLine, startCharacter, endLine, endCharacter),
            Source = "Sushi Compiler"
        };
    }

    private static (int Line, int Character) ToLSPPosition(SourceSnapshot snapshot, int byteOffset)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (byteOffset < 0 || byteOffset > snapshot.Bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        IReadOnlyList<int> lineStarts = snapshot.LineStarts;
        
        int low = 0;
        int high = lineStarts.Count - 1;
        int line = 0;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (lineStarts[middle] <= byteOffset)
            {
                line = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        int lineStart = lineStarts[line];

        ReadOnlySpan<byte> prefixBytes = snapshot.Bytes.Span[lineStart..byteOffset];

        string prefix = strictUtf8.GetString(prefixBytes);

        return (line, prefix.Length);
    }

    private void PublishDiagnosticsForAllDocuments(CancellationToken cancellationToken)
    {
        foreach (SourceDocument document in this.workspace.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!document.TryGetCurrentAnalysis(out AnalysisResult? analysis))
            {
                continue;
            }

            this.PublishDiagnostics(analysis);
        }
    }
}