using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Sushi.Diagnostics;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Precompilation;
using Sushi.Tokenization;
using Builder = System.Collections.Immutable.ImmutableArray<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>.Builder;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi codebases.
/// </summary>
public sealed class SushiLanguageService
{
    /// <summary>
    /// The workspace folders from the client. Contains the sushi project files when running in LSP mode.
    /// </summary>
    private readonly List<WorkspaceFolder> folders = [];

    /// <summary>
    /// The list of <see cref="TokenFile"/> objects that represent the lexed version of the source code.
    /// </summary>
    private readonly List<TokenFile> tokenFiles = [];

    /// <summary>
    /// Handles parsing <see cref="TokenFile"/> objects into an <see cref="AbstractSyntaxTree"/>.
    /// </summary>
    private readonly Parser parser = new();

    /// <summary>
    /// The <see cref="AbstractSyntaxTree"/> generated from parsing.
    /// </summary>
    private AbstractSyntaxTree tree = null!;

    /// <summary>
    /// Handles tracking and resolution of references, such as namespaces, variable names, and the like.
    /// </summary>
    private ReferenceResolver reference = null!;

    /// <summary>
    /// Initializes the language service with the information about the currently open workspace. Only used in LSP mode.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The folders that are a part of the workspace.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task InitializeWorkspace([NotNull] List<WorkspaceFolder> workspaceFolders)
    {
        await this.ReplaceWorkspaceFolders(workspaceFolders);
        await this.UpdateTokenFiles();
        this.tree = await this.parser.ParseFiles(this.tokenFiles);
        await this.UpdatePostParsing();
    }

    /// <summary>
    /// Runs a compile job using the current settings. This runs the whole lexing, parsing, and compilation stack (unless IntermediateOnly was passed in).
    /// Only used when not running in LSP mode.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CompileJob()
    {
        List<TokenFile> tokenFiles = await Lexer.LexFiles(AppMeta.Options.ProjectPath);

        AbstractSyntaxTree tree = await this.parser.ParseFiles(tokenFiles);

        await this.UpdatePostParsing();

        foreach (CompilerMessage message in (await this.GetMessages()).OrderBy(x => x.Type))
        {
            await message.LogMessage();
        }
    }

    /// <summary>
    /// Call this method whenever a <see cref="TokenFile"/> or <see cref="AbstractSyntaxTree"/> is modified to update everything
    /// down the chain that relies on it.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task UpdatePostParsing()
    {
        this.reference = new();
        await this.reference.Visit(this.tree);
    }

    /// <summary>
    /// Call this method whenever a <see cref="TokenFile"/> or <see cref="AbstractSyntaxTree"/> is modified to update everything
    /// down the chain that relies on it.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task UpdatePostParsing([NotNull] ILanguageServerFacade facade)
    {
        await this.UpdatePostParsing();

        await this.PublishDiagnosticsForAllDocuments(facade, null);
    }

    /// <summary>
    /// Updates the workspace folders with the current changeset from the changed workspace folders.
    /// </summary>
    /// <param name="facade">
    /// The language server facade.
    /// </param>
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
    public async Task UpdateWorkspaceFolders([NotNull] ILanguageServerFacade facade, [NotNull] List<WorkspaceFolder> addedFolders, [NotNull] List<WorkspaceFolder> removedFolders, CancellationToken cancellationToken)
    {
        this.tokenFiles.Clear();

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

        await this.UpdateTokenFiles();

        this.tree = await this.parser.ParseFiles(this.tokenFiles);

        await this.UpdatePostParsing(facade);
    }

    /// <summary>
    /// Replaces the current workspace folders with an entirely new set.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task ReplaceWorkspaceFolders([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders)
    {
        this.folders.Clear();
        this.folders.AddRange(workspaceFolders);
    }

    /// <summary>
    /// Updates the token files with the new source from disk.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task UpdateTokenFiles()
    {
        this.tokenFiles.Clear();

        foreach (WorkspaceFolder folder in this.folders)
        {
            this.tokenFiles.AddRange(await Lexer.LexFiles(folder.Uri.GetFileSystemPath()));
        }
    }

    /// <summary>
    /// Updates the source directly from disk.
    /// </summary>
    /// <param name="sourceFilePath">
    /// The file path of the source file to load from disk.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the new <see cref="TokenFile"/>.
    /// </returns>
    public async Task<TokenFile> UpdateSource([NotNull] string sourceFilePath)
    {
        TokenFile file = await Lexer.LexFile(sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => x.FilePath.IsSamePath(sourceFilePath));

        if (existingIndex != -1)
        {

            this.tokenFiles[existingIndex] = file;
        }

        return file;
    }

    /// <summary>
    /// Updates the source text of a specific document without going to disk. This usually happens
    /// when a document gets updated but isn't saved, so pulling it from disk wouldn't grab the changes.
    /// </summary>
    /// <param name="text">
    /// The text of the unsaved document.
    /// </param>
    /// <param name="sourceFilePath">
    /// The source file path to match it with the document uri it belongs to.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the new <see cref="TokenFile"/>.
    /// </returns>
    public async Task<TokenFile> UpdateSourceText([NotNull] string text, string sourceFilePath)
    {
        TokenFile file = await Lexer.LexStringAsFileText(text, sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => x.FilePath.IsSamePath(sourceFilePath));

        if (existingIndex != -1)
        {
            this.tokenFiles[existingIndex] = file;
        }

        return file;
    }

    /// <summary>
    /// Publishes diagnostic messages for all documents in the workspace.
    /// </summary>
    /// <param name="facade">
    /// The language server facade used to make the publish call.
    /// </param>
    /// <param name="version">
    /// Version numbers are used for concurrency.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task PublishDiagnosticsForAllDocuments([NotNull] ILanguageServerFacade facade, int? version)
    {
        List<CompilerMessage> messages = await this.GetMessages();

        List<DocumentUri> uris = [.. this.tokenFiles.Select(x => DocumentUri.FromFileSystemPath(x.FilePath))];

        foreach (IGrouping<string, CompilerMessage> group in messages.GroupBy(x => x.FilePath))
        {
            int existingIndex = uris.FindIndex(x => x.ToUri().IsSamePath(group.Key));

            if (existingIndex != -1)
            {
                uris.RemoveAt(existingIndex);
            }

            DocumentUri document = DocumentUri.FromFileSystemPath(group.Key);
            await PublishDiagnosticsForDocument(facade, version, [.. group], document);
        }

        foreach (DocumentUri uri in uris)
        {
            await PublishDiagnosticsForDocument(facade, version, [], uri);
        }
    }

    /// <summary>
    /// Publishes diagnostics for a single document.
    /// </summary>
    /// <param name="facade">
    /// The facade to make the publish call.
    /// </param>
    /// <param name="version">
    /// The version of the document update for concurrency.
    /// </param>
    /// <param name="messages">
    /// The messages to publish.
    /// </param>
    /// <param name="document">
    /// The document to publish the messages for.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task PublishDiagnosticsForDocument([NotNull] ILanguageServerFacade facade, int? version, List<CompilerMessage> messages, DocumentUri document)
    {
        Builder diagnostics = ImmutableArray<Diagnostic>.Empty.ToBuilder();

        foreach (CompilerMessage message in messages)
        {
            diagnostics.Add(await message.ToDiagnostic());
        }

        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams()
        {
            Diagnostics = new Container<Diagnostic>(diagnostics.ToArray()),
            Uri = document,
            Version = version
        });
    }

    /// <summary>
    /// Gets the <see cref="CompilerMessage"/> objects emitted by the compiler during the most recent update.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns a <see cref="List{T}"/> of <see cref="CompilerMessage"/>.
    /// </returns>
    private Task<List<CompilerMessage>> GetMessages() => Task.FromResult<List<CompilerMessage>>([..this.tree.GetMessages().Concat(this.tokenFiles.SelectMany(x => x.Messages))]);

    /// <summary>
    /// Updates the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="facade">The language server facade used to update the document.</param>
    /// <param name="textDocumentUri">
    /// The document uri.
    /// </param>
    /// <param name="version">
    /// The version number of the update request used for concurrency.
    /// </param>
    /// <param name="text">
    /// The text of the document so that it can be changed.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task UpdateDocument([NotNull] ILanguageServerFacade facade, [NotNull] DocumentUri textDocumentUri, int? version, string? text)
    {
        TokenFile file = !string.IsNullOrWhiteSpace(text)
            ? await this.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath)
            : await this.UpdateSource(textDocumentUri.ToUri().AbsolutePath);

        await this.parser.UpdateFile(file);

        await this.UpdatePostParsing(facade);

        await this.PublishDiagnosticsForAllDocuments(facade, version);
    }

    /// <summary>
    /// Adds the specified documents and publishes diagnostics for them.
    /// </summary>
    /// <param name="facade">
    /// The language server facade used to add the documents.
    /// </param>
    /// <param name="filePaths">
    /// The list of file paths.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task AddDocuments([NotNull] ILanguageServerFacade facade, [NotNull] List<Uri> filePaths)
    {
        foreach (Uri filePath in filePaths)
        {
            TokenFile file = await Lexer.LexFile(filePath.AbsolutePath);

            this.tokenFiles.Add(file);

            await this.parser.AddFile(file);
        }

        await this.UpdatePostParsing(facade);

        await this.PublishDiagnosticsForAllDocuments(facade, null);
    }

    /// <summary>
    /// Removes the specified documents and publishes diagnostics for the remaining files.
    /// </summary>
    /// <param name="facade">
    /// The language server facade used to remove the documents.
    /// </param>
    /// <param name="filePaths">
    /// The list of file paths.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task RemoveDocuments([NotNull] ILanguageServerFacade facade, [NotNull] List<Uri> filePaths)
    {
        foreach (Uri filePath in filePaths)
        {
            TokenFile? existing = this.tokenFiles.FirstOrDefault(x => x.FilePath.IsSamePath(filePath));

            if (existing is not null)
            {
                this.tokenFiles.Remove(existing);
                await this.parser.RemoveFile(existing);
            }
        }

        await this.UpdatePostParsing(facade);

        await this.PublishDiagnosticsForAllDocuments(facade, null);
    }
}