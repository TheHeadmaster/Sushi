using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Sushi.LanguageServerProtocol;
using Sushi.Source;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi code bases.
/// </summary>
public sealed class SushiLanguageService
{
    /// <summary>
    /// Contains source documents tied to their <see cref="Uri"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Uri, SourceDocument> documents = [];

    /// <summary>
    /// The workspace folders from the client. Contains the sushi project files when running in LSP mode.
    /// </summary>
    private readonly List<WorkspaceFolder> folders = [];

    private readonly WorkspaceOrchestrator workspace = new();

    /// <summary>
    /// Initializes the language service with the information about the currently open workspace. Only used in LSP mode.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The folders that are a part of the workspace.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task InitializeWorkspace([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders)
    {
        await this.ReplaceWorkspaceFolders(workspaceFolders);
        await UpdateSourceDocuments();
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
    /// Updates the workspace folders with the current changeset from the changed workspace folders.
    /// </summary>
    /// <param name="languageServer">
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
    public async Task UpdateWorkspaceFolders([NotNull] ILanguageServerFacade languageServer, [NotNull] IEnumerable<WorkspaceFolder> addedFolders, [NotNull] IEnumerable<WorkspaceFolder> removedFolders, CancellationToken cancellationToken)
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
    /// Runs the language server.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task RunLanguageServer()
    {
        switch (AppMeta.Options.LSPTransportMethod)
        {
            case LSPTransportMethod.Stdio:
            {
                await using Stream input = Console.OpenStandardInput();
                await using Stream output = Console.OpenStandardOutput();
                await this.RunLanguageServer(input, output);
                break;
            }
            case LSPTransportMethod.TCP:
                await this.RunTCPLanguageServer();
                break;
            default:
                InvalidOperationException exception = new("Tried to run Language Server without a valid transport mode.");
                Log.Error(exception, "Invalid LSP Transport {TransportMode}", AppMeta.Options.LSPTransportMethod);
                throw exception;
        }
    }

    /// <summary>
    /// Runs the language server with the specified input and output streams.
    /// </summary>
    /// <param name="input">
    /// The input stream.
    /// </param>
    /// <param name="output">
    /// The output stream.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task RunLanguageServer(Stream input, Stream output)
    {
        LanguageServer server = await LanguageServer.From(options => options
            .WithInput(input)
            .WithOutput(output)
            .WithServerInfo(new ServerInfo
            {
                Name = "Sushi",
                Version = AppMeta.GetVersion()
            })
            .OnInitialize((server, request, token) => this.InitializeWorkspace([.. request.WorkspaceFolders ?? []]))
            .WithServices(services => services.AddSingleton(this))
            //.WithHandler<WorkspaceFoldersHandler>()
            //.WithHandler<WorkspaceSymbolsHandler>()
            //.WithHandler<DeletedFileHandler>()
            //.WithHandler<CreatedFileHandler>()
            .WithHandler<TextDocumentSyncHandler>()
        );

        await server.WaitForExit;
    }

    private async Task RunTCPLanguageServer()
    {
        IPEndPoint? endpoint = AppMeta.Options.ListenEndpoint;

        if (endpoint is null)
        {
            InvalidOperationException exception = new("TCP endpoint was not configured.");
            Log.Error(exception, "ListenEndpoint should not be null when running the LSP in TCP mode.");
            throw exception;
        }

        TcpListener listener = new(endpoint);

        try
        {
            listener.Start();

            Log.Information("Listening for LSP client on {Endpoint}.", endpoint);

            using TcpClient client = await listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();

            await this.RunLanguageServer(stream, stream);
        }
        finally
        {
            listener.Stop();
            listener.Dispose();
        }
    }

    /// <summary>
    /// Updates the specified document and publishes diagnostics for it.
    /// </summary>
    /// <param name="languageServer">
    /// The language server facade used to update the document.
    /// </param>
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
    public async Task UpdateDocument([NotNull] ILanguageServerFacade languageServer, [NotNull] Uri documentUri, int? version, string? text, CancellationToken cancellationToken)
    {
        TokenFile file = !string.IsNullOrWhiteSpace(text)
            ? await this.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath)
            : await this.UpdateSource(textDocumentUri.ToUri().AbsolutePath);

        await this.parser.UpdateFile(file);

        await this.UpdatePostParsing(facade);

        await this.PublishDiagnosticsForAllDocuments(facade, version);
    }

    /// <summary>
    /// Runs a compile job using the current settings. This runs the whole lexing, parsing, and compilation stack.
    /// Only used when not running in LSP mode.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CompileJob()
    {
        // Stub
    }

    /// <summary>
    /// Publishes diagnostic messages for all documents in the workspace.
    /// </summary>
    /// <param name="languageServer">
    /// The language server facade used to make the publish call.
    /// </param>
    /// <param name="version">
    /// Version numbers are used for concurrency.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private async Task PublishDiagnosticsForAllDocuments([NotNull] ILanguageServerFacade languageServer, int? version)
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
            await PublishDiagnosticsForDocument(languageServer, version, [.. group], document);
        }

        foreach (DocumentUri uri in uris)
        {
            await PublishDiagnosticsForDocument(languageServer, version, [], uri);
        }
    }
}