using System.Diagnostics.CodeAnalysis;
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
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Orchestrates updates and changes tracking for the current open workspace.
/// </summary>
public sealed class WorkspaceOrchestrator : IDisposable
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

    private readonly ProjectDiagnosticAnalyzer projectDiagnosticAnalyzer = new();

    private readonly object foldersSyncRoot = new();

    private readonly SemaphoreSlim mutationGate = new(1, 1);

    private bool disposed;

    private long workspaceGeneration;

    private long analyzedWorkspaceGeneration = -1;

    private long cachedHoverGeneration = -1;

    private IReadOnlyDictionary<SourceDocument, SourceAnalysisResult>? cachedHoverAnalyses;

    private long cachedSemanticAnalysisGeneration = -1;

    private int parameterInlayHintMode = (int)ParameterInlayHintMode.Selective;

    private IReadOnlyList<SourceAnalysisResult>? cachedSemanticAnalyses;

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

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            this.ReplaceWorkspaceFolders(workspaceFolders);

            await this.UpdateSourceDocuments(cancellationToken);
            this.AdvanceWorkspaceGeneration();
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeWorkspace(cancellationToken);
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

            await this.UpdateSourceDocuments(cancellationToken);
            this.AdvanceWorkspaceGeneration();
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeWorkspace(cancellationToken);
    }

    private async Task UpdateSourceDocuments(CancellationToken cancellationToken)
    {
        WorkspaceFolder[] folders;

        lock (this.foldersSyncRoot)
        {
            folders = [.. this.folders];
        }

        List<Uri> discoveredDocuments = [];

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

            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (SourceDocument document in this.workspace.Documents)
        {
            bool stillExists = discoveredDocuments.Any(uri => uri.IsSamePath(document.Uri));

            if (!stillExists && !document.IsOpen)
            {
                this.CancelScheduledAnalysis();

                this.workspace.RemoveDocument(document.Uri);

                this.ClearDiagnostics(document.Uri);
            }
        }

    }

    private static bool IsWorkspaceDocument(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        return extension.Equals(".sus", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".susln", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExtension(SourceSnapshot snapshot, string extension)
        => Path.GetExtension(snapshot.Uri.LocalPath).Equals(extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Captures authoritative inspection inputs under the mutation gate, without retaining that gate during compilation.
    /// </summary>
    /// <param name="documentUri">
    /// The ordinary Sushi source document selected by the client.
    /// </param>
    /// <param name="version">
    /// The client version that must match the current snapshot when supplied.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels waiting for workspace synchronization.
    /// </param>
    /// <returns>
    /// Frozen snapshot references and deterministic project candidates for this request.
    /// </returns>
    internal async Task<InspectionWorkspaceSnapshot> CaptureInspectionInputs(Uri documentUri, int? version, CancellationToken cancellationToken)
    {
        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase)
                || !this.workspace.TryGetDocument(documentUri, out SourceDocument? document))
            {
                throw new InspectionInputException("Open an ordinary Sushi source file before requesting inspection.");
            }

            SourceSnapshot selected = document.CurrentSnapshot;
            if (version is not null && selected.Version != version)
            {
                throw new InspectionInputException("The source version has not synchronized with the language server. Retry inspection.");
            }

            List<SourceSnapshot> open = [];
            List<Uri> projects = [];
            foreach (SourceDocument item in this.workspace.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string extension = Path.GetExtension(item.Uri.LocalPath);
                if (item.IsOpen && extension.Equals(".sus", StringComparison.OrdinalIgnoreCase))
                {
                    open.Add(item.CurrentSnapshot);
                }
                else if (extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase))
                {
                    projects.Add(item.Uri);
                }
            }

            return new InspectionWorkspaceSnapshot(selected, Array.AsReadOnly(open.ToArray()),
                Array.AsReadOnly(projects.OrderBy(uri => uri.LocalPath, StringComparer.Ordinal).ToArray()));
        }
        finally
        {
            this.mutationGate.Release();
        }
    }

    /// <summary>
    /// Gets current project-aware semantic occurrences for source Hover without publishing diagnostics.
    /// </summary>
    /// <param name="documentUri">The ordinary Sushi source document being hovered.</param>
    /// <param name="cancellationToken">Cancels snapshot capture, project analysis, or stale-result validation.</param>
    /// <returns>Analysis for one still-current source snapshot, or null when project semantics are ambiguous or unavailable.</returns>
    internal async Task<SourceAnalysisResult?> GetSourceHoverAnalysis(Uri documentUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        WorkspaceAnalysisInput input;
        SourceDocument selectedDocument;
        SourceSnapshot selectedSnapshot;
        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!this.workspace.TryGetDocument(documentUri, out SourceDocument? existing))
            {
                return null;
            }

            selectedDocument = existing;
            selectedSnapshot = selectedDocument.CurrentSnapshot;
            if (this.cachedHoverGeneration == this.workspaceGeneration
                && this.cachedHoverAnalyses is not null
                && this.cachedHoverAnalyses.TryGetValue(selectedDocument, out SourceAnalysisResult? cachedAnalysis)
                && ReferenceEquals(cachedAnalysis.Snapshot, selectedSnapshot))
            {
                return cachedAnalysis;
            }

            if (this.analyzedWorkspaceGeneration == this.workspaceGeneration
                && selectedDocument.TryGetCurrentAnalysis(out AnalysisResult? current, out _)
                && current is SourceAnalysisResult analyzed)
            {
                this.cachedHoverAnalyses = new Dictionary<SourceDocument, SourceAnalysisResult>(ReferenceEqualityComparer.Instance)
                {
                    [selectedDocument] = analyzed
                };
                this.cachedHoverGeneration = this.workspaceGeneration;
                return analyzed;
            }

            input = new WorkspaceAnalysisInput(this.workspaceGeneration,
                Array.AsReadOnly(this.workspace.Documents.Select(document => new DocumentSnapshot(document, document.CurrentSnapshot)).ToArray()));
        }
        finally
        {
            this.mutationGate.Release();
        }

        IReadOnlyList<ProjectDiagnosticResult> results = await this.projectDiagnosticAnalyzer.AnalyzeAsync(
            input.Documents.Select(item => item.Snapshot).ToArray(), [documentUri], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SourceAnalysisResult? result = results.Select(item => item.Analysis)
            .OfType<SourceAnalysisResult>()
            .FirstOrDefault(item => ReferenceEquals(item.Snapshot, selectedSnapshot));
        if (result is null)
        {
            return null;
        }

        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (this.workspaceGeneration != input.Generation || !selectedDocument.IsCurrent(selectedSnapshot)
                || input.Documents.Any(item => !item.Document.IsCurrent(item.Snapshot)))
            {
                return null;
            }

            Dictionary<SourceDocument, SourceAnalysisResult> analyses = new(ReferenceEqualityComparer.Instance);
            Dictionary<SourceSnapshot, SourceAnalysisResult> analysesBySnapshot = new(ReferenceEqualityComparer.Instance);
            foreach (SourceAnalysisResult sourceAnalysis in results.Select(diagnostic => diagnostic.Analysis).OfType<SourceAnalysisResult>())
            {
                analysesBySnapshot[sourceAnalysis.Snapshot] = sourceAnalysis;
            }

            foreach (DocumentSnapshot item in input.Documents)
            {
                if (analysesBySnapshot.TryGetValue(item.Snapshot, out SourceAnalysisResult? sourceAnalysis))
                {
                    analyses.Add(item.Document, sourceAnalysis);
                }
            }

            this.cachedHoverAnalyses = analyses;
            this.cachedHoverGeneration = input.Generation;
            this.cachedSemanticAnalyses = Array.AsReadOnly(results.Select(item => item.Analysis)
                .OfType<SourceAnalysisResult>().ToArray());
            this.cachedSemanticAnalysisGeneration = input.Generation;
            return result;
        }
        finally
        {
            this.mutationGate.Release();
        }
    }

    /// <summary>
    /// Gets one coherent project-aware analysis set for source Find All References.
    /// </summary>
    /// <param name="documentUri">
    /// The Sushi source document containing the selected occurrence.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels snapshot capture, semantic analysis, or final validation.
    /// </param>
    /// <returns>
    /// The selected analysis and current semantic context, or null when no source analysis is available.
    /// </returns>
    internal async Task<SourceSemanticAnalysisSet?> GetSourceReferenceAnalysisSet(Uri documentUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        WorkspaceAnalysisInput input;
        SourceDocument selectedDocument;
        SourceSnapshot selectedSnapshot;
        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (!this.workspace.TryGetDocument(documentUri, out SourceDocument? existing))
            {
                return null;
            }

            selectedDocument = existing;
            selectedSnapshot = selectedDocument.CurrentSnapshot;
            input = new WorkspaceAnalysisInput(this.workspaceGeneration,
                Array.AsReadOnly(this.workspace.Documents.Select(document => new DocumentSnapshot(document, document.CurrentSnapshot)).ToArray()));

            if (this.cachedSemanticAnalysisGeneration == this.workspaceGeneration
                && this.cachedSemanticAnalyses is not null)
            {
                SourceAnalysisResult? cachedSelected = this.cachedSemanticAnalyses.FirstOrDefault(
                    analysis => ReferenceEquals(analysis.Snapshot, selectedSnapshot));
                if (cachedSelected is not null)
                {
                    return new SourceSemanticAnalysisSet(input.Generation, cachedSelected, this.cachedSemanticAnalyses,
                        Array.AsReadOnly(input.Documents.Select(item => item.Snapshot).ToArray()),
                        Array.AsReadOnly(input.Documents.Where(item => item.Document.IsOpen).Select(item => item.Snapshot.Uri).ToArray()));
                }
            }
        }
        finally
        {
            this.mutationGate.Release();
        }

        IReadOnlyList<ProjectDiagnosticResult> results = await this.projectDiagnosticAnalyzer.AnalyzeAsync(
            input.Documents.Select(item => item.Snapshot).ToArray(), [documentUri], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SourceAnalysisResult> analyses = Array.AsReadOnly(results.Select(item => item.Analysis)
            .OfType<SourceAnalysisResult>().ToArray());
        SourceAnalysisResult? selectedAnalysis = analyses.FirstOrDefault(analysis => ReferenceEquals(analysis.Snapshot, selectedSnapshot));
        if (selectedAnalysis is null)
        {
            return null;
        }

        SourceSemanticAnalysisSet result = new(input.Generation, selectedAnalysis, analyses,
            Array.AsReadOnly(input.Documents.Select(item => item.Snapshot).ToArray()),
            Array.AsReadOnly(input.Documents.Where(item => item.Document.IsOpen).Select(item => item.Snapshot.Uri).ToArray()));
        if (!await this.IsCurrentSourceAnalysisSetAsync(result, cancellationToken))
        {
            return null;
        }

        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (this.workspaceGeneration != input.Generation
                || input.Documents.Any(item => !item.Document.IsCurrent(item.Snapshot)))
            {
                return null;
            }

            this.cachedSemanticAnalyses = analyses;
            this.cachedSemanticAnalysisGeneration = input.Generation;
        }
        finally
        {
            this.mutationGate.Release();
        }

        return result;
    }

    /// <summary>
    /// Gets the currently configured parameter inlay hint mode.
    /// </summary>
    internal ParameterInlayHintMode ParameterInlayHintMode
        => (ParameterInlayHintMode)Volatile.Read(ref this.parameterInlayHintMode);

    /// <summary>
    /// Changes parameter hint presentation and requests a standard LSP refresh when the mode changes.
    /// </summary>
    /// <param name="mode">The newly selected presentation mode.</param>
    /// <param name="cancellationToken">Cancels the client refresh request.</param>
    internal Task SetParameterInlayHintModeAsync(ParameterInlayHintMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int previous = Interlocked.Exchange(ref this.parameterInlayHintMode, (int)mode);
        ILanguageServerFacade? languageServer = this.languageServer;
        if (previous != (int)mode && languageServer is not null)
        {
            languageServer.Workspace.SendInlayHintRefresh(new InlayHintRefreshParams());
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current project-aware source analysis for parameter inlay hints.
    /// </summary>
    /// <param name="documentUri">The source document whose invocation arguments are requested.</param>
    /// <param name="cancellationToken">Cancels semantic analysis and freshness validation.</param>
    /// <returns>The analysis when its source and project context remain current.</returns>
    internal async Task<SourceSemanticAnalysisSet?> GetParameterInlayHintAnalysisSet(Uri documentUri, CancellationToken cancellationToken)
    {
        SourceSemanticAnalysisSet? analysisSet = await this.GetSourceReferenceAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null || !await this.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return null;
        }

        return analysisSet;
    }

    /// <summary>
    /// Confirms that every captured workspace snapshot and project member remains current.
    /// </summary>
    /// <param name="analysisSet">
    /// The immutable project-wide analysis captured for a references request.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels disk freshness checks or workspace validation.
    /// </param>
    /// <returns>
    /// True only while the captured source and project inputs are still authoritative.
    /// </returns>
    internal async Task<bool> IsCurrentSourceAnalysisSetAsync(SourceSemanticAnalysisSet analysisSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysisSet);
        if (!await IsProjectMembershipCurrent(analysisSet, cancellationToken))
        {
            return false;
        }

        foreach (SourceAnalysisResult analysis in GetRelevantAnalyses(analysisSet))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isCapturedWorkspaceDocument = analysisSet.WorkspaceSnapshots.Any(snapshot =>
                ReferenceEquals(snapshot, analysis.Snapshot));
            bool isOpenDocument = analysisSet.OpenDocumentUris.Any(uri => uri.IsSamePath(analysis.Snapshot.Uri));
            if (isCapturedWorkspaceDocument && !isOpenDocument
                && !await IsDiskSnapshotCurrent(analysis.Snapshot, cancellationToken))
            {
                return false;
            }

            if (!isCapturedWorkspaceDocument && !await IsDiskSnapshotCurrent(analysis.Snapshot, cancellationToken))
            {
                return false;
            }
        }

        if (analysisSet.Selected.SemanticProjectUri is Uri projectUri)
        {
            SourceSnapshot? projectSnapshot = analysisSet.WorkspaceSnapshots.FirstOrDefault(snapshot => snapshot.Uri.IsSamePath(projectUri));
            bool projectIsOpen = analysisSet.OpenDocumentUris.Any(uri => uri.IsSamePath(projectUri));
            if (projectSnapshot is null || (!projectIsOpen && !await IsDiskSnapshotCurrent(projectSnapshot, cancellationToken)))
            {
                return false;
            }
        }

        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (this.workspaceGeneration != analysisSet.Generation)
            {
                return false;
            }

            foreach (SourceSnapshot snapshot in analysisSet.WorkspaceSnapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!this.workspace.TryGetDocument(snapshot.Uri, out SourceDocument? document) || !document.IsCurrent(snapshot))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            this.mutationGate.Release();
        }
    }

    private static async Task<bool> IsProjectMembershipCurrent(SourceSemanticAnalysisSet analysisSet, CancellationToken cancellationToken)
    {
        if (analysisSet.Selected.SemanticProjectUri is not Uri projectUri)
        {
            return await IsStandaloneMembershipCurrent(analysisSet, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        SourceSnapshot? projectSnapshot = analysisSet.WorkspaceSnapshots.FirstOrDefault(snapshot => snapshot.Uri.IsSamePath(projectUri));
        if (projectSnapshot is null)
        {
            return false;
        }

        DiagnosticReporter reporter = new();
        ProjectAnalysisResult project = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(projectSnapshot, reporter, cancellationToken);
        if (project.Project is null)
        {
            return false;
        }

        string projectDirectory = Path.GetDirectoryName(projectUri.LocalPath)!;
        StringComparer pathComparer = PathComparer;
        HashSet<string> currentMembers = new(pathComparer);
        foreach (string path in ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, project.Project, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentMembers.Add(Path.GetFullPath(path));
        }

        HashSet<string> analyzedMembers = new(pathComparer);
        foreach (SourceAnalysisResult analysis in GetRelevantAnalyses(analysisSet))
        {
            cancellationToken.ThrowIfCancellationRequested();
            analyzedMembers.Add(Path.GetFullPath(analysis.Snapshot.Uri.LocalPath));
        }

        return currentMembers.SetEquals(analyzedMembers);
    }

    private static bool IsWithinDirectory(Uri uri, string directory)
    {
        string fullDirectory = Path.GetFullPath(directory);
        string fullPath = Path.GetFullPath(uri.LocalPath);
        string relative = Path.GetRelativePath(fullDirectory, fullPath);
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return !Path.IsPathRooted(relative) && !relative.Equals("..", pathComparison)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, pathComparison);
    }

    private static async Task<bool> IsStandaloneMembershipCurrent(SourceSemanticAnalysisSet analysisSet, CancellationToken cancellationToken)
    {
        string selectedPath = Path.GetFullPath(analysisSet.Selected.Snapshot.Uri.LocalPath);
        foreach (SourceSnapshot candidate in analysisSet.WorkspaceSnapshots.Where(snapshot =>
            Path.GetExtension(snapshot.Uri.LocalPath).Equals(".susproj", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string projectDirectory = Path.GetDirectoryName(candidate.Uri.LocalPath)!;
            if (!IsWithinDirectory(analysisSet.Selected.Snapshot.Uri, projectDirectory))
            {
                continue;
            }

            bool isOpen = analysisSet.OpenDocumentUris.Any(uri => uri.IsSamePath(candidate.Uri));
            if (!isOpen && !await IsDiskSnapshotCurrent(candidate, cancellationToken))
            {
                return false;
            }

            DiagnosticReporter reporter = new();
            ProjectAnalysisResult project = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(candidate, reporter, cancellationToken);
            if (project.Project is null)
            {
                return false;
            }

            IReadOnlyList<string> members = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, project.Project, cancellationToken);
            foreach (string member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (PathComparer.Equals(Path.GetFullPath(member), selectedPath))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static IEnumerable<SourceAnalysisResult> GetRelevantAnalyses(SourceSemanticAnalysisSet analysisSet)
        => analysisSet.Selected.SemanticProjectUri is Uri projectUri
            ? analysisSet.Analyses.Where(analysis => analysis.SemanticProjectUri is Uri candidate && candidate.IsSamePath(projectUri))
            : new[] { analysisSet.Selected };

    private static async Task<bool> IsDiskSnapshotCurrent(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!snapshot.Uri.IsFile || !File.Exists(snapshot.Uri.LocalPath))
        {
            return false;
        }

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(snapshot.Uri.LocalPath, cancellationToken);
            SourceSnapshot diskSnapshot = SourceSnapshot.FromUtf8(snapshot.Uri, version: null, bytes);
            return diskSnapshot.IsValidUtf8 == snapshot.IsValidUtf8
                && snapshot.Bytes.Span.SequenceEqual(diskSnapshot.Bytes.Span);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Confirms that a source-semantic result still represents the current workspace context.
    /// </summary>
    /// <param name="analysis">The result whose source and project dependencies must remain current.</param>
    /// <param name="cancellationToken">Cancels revalidation if the workspace changed.</param>
    /// <returns>True only when current analysis reuses the exact result instance.</returns>
    internal async Task<bool> IsCurrentSourceAnalysisAsync(SourceAnalysisResult analysis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        SourceAnalysisResult? current = await this.GetSourceHoverAnalysis(analysis.Snapshot.Uri, cancellationToken);
        return ReferenceEquals(current, analysis);
    }

    private void AdvanceWorkspaceGeneration()
    {
        this.workspaceGeneration++;
        this.cachedHoverGeneration = -1;
        this.cachedHoverAnalyses = null;
        this.cachedSemanticAnalysisGeneration = -1;
        this.cachedSemanticAnalyses = null;
    }

    /// <summary>
    /// Gets current Sushi syntax for semantic highlighting. Reuses an analyzed
    /// syntax tree when it belongs to the active snapshot, otherwise parses the
    /// current editor snapshot without waiting for debounced semantic analysis.
    /// </summary>
    /// <param name="documentUri">
    /// The Sushi source document requested by the language client.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels snapshot retrieval or parsing.
    /// </param>
    /// <returns>
    /// Parsed syntax for the current source snapshot, or null when the document
    /// is absent or was superseded while parsing.
    /// </returns>
    public async Task<SourceFileSyntax?> GetSemanticSyntax(Uri documentUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);
        cancellationToken.ThrowIfCancellationRequested();

        SourceDocument document;
        SourceSnapshot snapshot;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            if (!this.workspace.TryGetDocument(documentUri, out SourceDocument? existing))
            {
                return null;
            }

            document = existing;
            snapshot = document.CurrentSnapshot;

            if (document.TryGetCurrentAnalysis(out AnalysisResult? analysis, out _)
                && analysis is SourceAnalysisResult sourceAnalysis)
            {
                return sourceAnalysis.SyntaxTree.Root as SourceFileSyntax;
            }
        }
        finally
        {
            this.mutationGate.Release();
        }

        IDiagnosticReporter diagnostics = new DiagnosticReporter();
        LexerResult lexicalResult = new SourceLexer().Lex(snapshot, diagnostics, cancellationToken);
        ParserResult parsed = SourceParser.ParseSourceFile(lexicalResult, diagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!document.IsCurrent(snapshot))
        {
            return null;
        }

        return parsed.Tree.Root as SourceFileSyntax;
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

            editorSnapshot = SourceSnapshot.FromText(documentUri, version, text);

            document.UpdateEditor(editorSnapshot);
            if (!document.IsCurrent(editorSnapshot))
            {
                return;
            }

            this.AdvanceWorkspaceGeneration();
            this.CancelScheduledAnalysis();
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeWorkspace(cancellationToken);
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
        SourceSnapshot snapshot = SourceSnapshot.FromText(documentUri, version, text);

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);
            document.UpdateEditor(snapshot);
            if (!document.IsCurrent(snapshot))
            {
                return;
            }

            this.AdvanceWorkspaceGeneration();
            this.ScheduleWorkspaceAnalysis(editorAnalysisDebounce, [documentUri]);
        }
        finally
        {
            this.mutationGate.Release();
        }
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

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);
            SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);
            
            document.UpdateDisk(diskSnapshot);

            this.AdvanceWorkspaceGeneration();
            this.CancelScheduledAnalysis();
        }
        finally
        {
            this.mutationGate.Release();
        }

        await this.AnalyzeWorkspace(cancellationToken);
    }

    private static async Task<SourceSnapshot> LoadDiskSnapshot(Uri documentUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentUri);

        if (!documentUri.IsFile)
        {
            throw new ArgumentException("Only file URIs can be loaded from disk.", nameof(documentUri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = await File.ReadAllBytesAsync(documentUri.LocalPath, cancellationToken);

        return SourceSnapshot.FromUtf8(documentUri, version: null, bytes);
    }

    private async Task AnalyzeWorkspace(CancellationToken cancellationToken, IReadOnlyList<Uri>? affectedUris = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceAnalysisInput input;

        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            input = new WorkspaceAnalysisInput(this.workspaceGeneration,
                [.. this.workspace.Documents.Select(document => new DocumentSnapshot(document, document.CurrentSnapshot))]);
        }
        finally
        {
            this.mutationGate.Release();
        }

        IReadOnlyList<SourceSnapshot> snapshots = [.. input.Documents.Select(item => item.Snapshot)];
        bool analyzeEntireWorkspace = affectedUris is null || affectedUris.Any(uri =>
            !Path.GetExtension(uri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase) || !File.Exists(uri.LocalPath));
        IReadOnlyList<Uri>? effectiveAffectedUris = analyzeEntireWorkspace ? null : affectedUris;
        List<ProjectDiagnosticResult> results = [.. await this.projectDiagnosticAnalyzer.AnalyzeAsync(snapshots, effectiveAffectedUris, cancellationToken)];
        foreach (DocumentSnapshot item in input.Documents.Where(item => IsExtension(item.Snapshot, ".susln")
            && (effectiveAffectedUris is null || effectiveAffectedUris.Any(uri => uri.IsSamePath(item.Snapshot.Uri)))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDiagnosticReporter reporter = new DiagnosticReporter();
            AnalysisResult analysis = await this.analyzer.Analyze(item.Snapshot, reporter, cancellationToken);
            results.Add(new ProjectDiagnosticResult(analysis, reporter));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await this.mutationGate.WaitAsync(cancellationToken);
        try
        {
            if (this.workspaceGeneration != input.Generation
                || input.Documents.Any(item => !item.Document.IsCurrent(item.Snapshot)))
            {
                return;
            }

            foreach (ProjectDiagnosticResult result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DocumentSnapshot? item = input.Documents.FirstOrDefault(candidate => ReferenceEquals(candidate.Snapshot, result.Analysis.Snapshot));
                if (item is null || !item.Document.TryUpdateAnalysis(result.Analysis, result.Reporter))
                {
                    continue;
                }

                this.PublishDiagnostics(result.Analysis, result.Reporter.ReportDiagnostics());
            }

            this.analyzedWorkspaceGeneration = input.Generation;
            if (effectiveAffectedUris is null)
            {
                this.cachedSemanticAnalyses = Array.AsReadOnly(results.Select(item => item.Analysis)
                    .OfType<SourceAnalysisResult>().ToArray());
                this.cachedSemanticAnalysisGeneration = input.Generation;
            }
        }
        finally
        {
            this.mutationGate.Release();
        }
    }

    private sealed record WorkspaceAnalysisInput(long Generation, IReadOnlyList<DocumentSnapshot> Documents);

    private sealed record DocumentSnapshot(SourceDocument Document, SourceSnapshot Snapshot);

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

    private static readonly TimeSpan fileSystemAnalysisDebounce = TimeSpan.FromMilliseconds(500);

    private readonly object analysisScheduleSyncRoot = new();

    private CancellationTokenSource? scheduledWorkspaceAnalysis;

    private HashSet<Uri>? scheduledAnalysisUris;

    private ILanguageServerFacade? languageServer;

    private void ScheduleWorkspaceAnalysis(TimeSpan delay, IEnumerable<Uri> affectedUris)
    {
        ArgumentNullException.ThrowIfNull(affectedUris);
        CancellationTokenSource cancellation = new();
        Uri[] scope;

        lock (this.analysisScheduleSyncRoot)
        {
            this.scheduledWorkspaceAnalysis?.Cancel();
            this.scheduledAnalysisUris ??= [];
            foreach (Uri uri in affectedUris)
            {
                this.scheduledAnalysisUris.Add(uri);
            }

            scope = [.. this.scheduledAnalysisUris];
            this.scheduledWorkspaceAnalysis = cancellation;
        }

        _ = this.RunScheduledWorkspaceAnalysis(delay, cancellation, scope);
    }

    private async Task RunScheduledWorkspaceAnalysis(TimeSpan delay, CancellationTokenSource cancellation, IReadOnlyList<Uri> affectedUris)
    {
        try
        {
            await Task.Delay(delay, cancellation.Token);
            await this.AnalyzeWorkspace(cancellation.Token, affectedUris);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Expected when a newer workspace snapshot supersedes this analysis.
        }
        
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled exception while performing scheduled project diagnostics.");
        }
#pragma warning restore CA1031 // Do not catch general exception types
        finally
        {
            lock (this.analysisScheduleSyncRoot)
            {
                if (ReferenceEquals(this.scheduledWorkspaceAnalysis, cancellation))
                {
                    this.scheduledWorkspaceAnalysis = null;
                    this.scheduledAnalysisUris = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void CancelScheduledAnalysis()
    {
        lock (this.analysisScheduleSyncRoot)
        {
            if (this.scheduledWorkspaceAnalysis is CancellationTokenSource cancellation)
            {
                this.scheduledWorkspaceAnalysis = null;
                this.scheduledAnalysisUris = null;
                cancellation.Cancel();
            }
        }
    }

    private void PublishDiagnostics(AnalysisResult result, IReadOnlyList<SushiDiagnostic> sushiDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (this.languageServer is null)
        {
            return;
        }

        SourceSnapshot snapshot = result.Snapshot;

        Diagnostic[] diagnostics = [.. sushiDiagnostics.Select(diagnostic => ToLSPDiagnostic(diagnostic, snapshot))];

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
            throw new InvalidOperationException("Diagnostic belongs to a different source snapshot.");
        }

        (int startLine, int startCharacter) = span.Snapshot.GetUtf16Position(span.Start);
        (int endLine, int endCharacter) = span.Snapshot.GetUtf16Position(span.End);

        return new Diagnostic
        {
            Code = diagnostic.DisplayName,
            Message = diagnostic.Message,
            Severity = ToLspSeverity(diagnostic.Severity),
            Range = new LSPRange(startLine, startCharacter, endLine, endCharacter),
            Source = "Sushi Compiler"
        };
    }

    private void PublishDiagnosticsForAllDocuments(CancellationToken cancellationToken)
    {
        foreach (SourceDocument document in this.workspace.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!document.TryGetCurrentAnalysis(out AnalysisResult? analysis, out IReadOnlyList<SushiDiagnostic>? diagnostics))
            {
                continue;
            }

            this.PublishDiagnostics(analysis, diagnostics);
        }
    }

    public async Task ApplyFileChanges([NotNull] IEnumerable<FileEvent> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        FileEvent[] fileEvents = [.. changes];
        bool changed = false;
        List<Uri> affectedUris = [];

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            foreach (FileEvent fileEvent in fileEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Uri documentUri = fileEvent.Uri.ToUri();

                if (!documentUri.IsFile || !IsWorkspaceDocument(documentUri.LocalPath))
                {
                    continue;
                }

                switch (fileEvent.Type)
                {
                    case FileChangeType.Created:
                    case FileChangeType.Changed:
                        if (await this.RefreshDiskDocument(documentUri, cancellationToken))
                        {
                            changed = true;
                            affectedUris.Add(documentUri);
                        }
                        break;
                    case FileChangeType.Deleted:
                        if (this.HandleDeletedDiskDocument(documentUri))
                        {
                            changed = true;
                            affectedUris.Add(documentUri);
                        }
                        break;
                    default:
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                        throw new ArgumentOutOfRangeException(nameof(fileEvent), fileEvent.Type, "Unsupported file change type.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
                }
            }

            if (changed)
            {
                this.AdvanceWorkspaceGeneration();
            }
        }
        finally
        {
            this.mutationGate.Release();
        }

        if (changed)
        {
            this.ScheduleWorkspaceAnalysis(fileSystemAnalysisDebounce, affectedUris);
        }
    }

    private async Task<bool> RefreshDiskDocument(Uri documentUri, CancellationToken cancellationToken)
    {
        if (!File.Exists(documentUri.LocalPath))
        {
            return this.HandleDeletedDiskDocument(documentUri);
        }

        SourceSnapshot diskSnapshot;

        try
        {
            diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);
        }
        catch (Exception _) when (_ is FileNotFoundException or DirectoryNotFoundException)
        {
            return this.HandleDeletedDiskDocument(documentUri);
        }

        SourceDocument document = this.workspace.GetOrAddDocument(documentUri, diskSnapshot);
        document.UpdateDisk(diskSnapshot);

        if (document.IsOpen)
        {
            return false;
        }

        return true;
    }

    private bool HandleDeletedDiskDocument(Uri documentUri)
    {
        if (!this.workspace.TryGetDocument(documentUri, out SourceDocument? document))
        {
            this.ClearDiagnostics(documentUri);

            return false;
        }

        if (document.IsOpen)
        {
            // The in-memory editor snapshot remains authoritative.
            // The stale disk snapshot will never become current while
            // the editor snapshot exists.
            return false;
        }

        this.CancelScheduledAnalysis();
        this.workspace.RemoveDocument(documentUri);
        this.ClearDiagnostics(documentUri);
        return true;
    }

    private void ClearDiagnostics(Uri documentUri)
    {
        ArgumentNullException.ThrowIfNull(documentUri);

        if (this.languageServer is null)
        {
            return;
        }

        this.languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.FromFileSystemPath(documentUri.LocalPath),
            Version = null,
            Diagnostics = new Container<Diagnostic>([])
        });
    }

    public async Task CloseDocument([NotNull] Uri documentUri, CancellationToken cancellationToken)
    {
        SourceDocument? document = null;
        SourceSnapshot? snapshot = null;

        bool removed = false;

        await this.mutationGate.WaitAsync(cancellationToken);

        try
        {
            document = this.workspace.GetDocument(documentUri);

            this.CancelScheduledAnalysis();

            if (!File.Exists(documentUri.LocalPath))
            {
                this.workspace.RemoveDocument(documentUri);

                this.ClearDiagnostics(documentUri);

                removed = true;
            }
            else
            {
                SourceSnapshot diskSnapshot = await LoadDiskSnapshot(documentUri, cancellationToken);

                document.Close(diskSnapshot);

                snapshot = document.CurrentSnapshot;
            }

            this.AdvanceWorkspaceGeneration();
        }
        finally
        {
            this.mutationGate.Release();
        }

        if (!removed)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(snapshot);
        }

        await this.AnalyzeWorkspace(cancellationToken);
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        lock (this.analysisScheduleSyncRoot)
        {
            this.scheduledWorkspaceAnalysis?.Cancel();
            this.scheduledWorkspaceAnalysis?.Dispose();
            this.scheduledWorkspaceAnalysis = null;
            this.scheduledAnalysisUris = null;
        }

        this.cachedHoverAnalyses = null;
        this.cachedHoverGeneration = -1;
        this.cachedSemanticAnalyses = null;
        this.cachedSemanticAnalysisGeneration = -1;
        this.mutationGate.Dispose();

        this.disposed = true;
    }
}

/// <summary>
/// Holds project-aware source analyses captured from one workspace generation.
/// </summary>
/// <param name="Generation">
/// The workspace generation used by every analysis in the set.
/// </param>
/// <param name="Selected">
/// The analysis containing the requested cursor position.
/// </param>
/// <param name="Analyses">
/// Source analyses produced from the same project and editor snapshot capture.
/// </param>
/// <param name="WorkspaceSnapshots">
/// All authoritative document snapshots captured before analysis began.
/// </param>
/// <param name="OpenDocumentUris">
/// The paths whose authoritative snapshots came from unsaved editor buffers.
/// </param>
internal sealed record SourceSemanticAnalysisSet(
    long Generation,
    SourceAnalysisResult Selected,
    IReadOnlyList<SourceAnalysisResult> Analyses,
    IReadOnlyList<SourceSnapshot> WorkspaceSnapshots,
    IReadOnlyList<Uri> OpenDocumentUris);
