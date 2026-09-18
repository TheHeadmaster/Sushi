using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi code bases.
/// </summary>
public sealed class SushiLanguageService
{
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
        // Stub
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
                Stream input = Console.OpenStandardInput();
                Stream output = Console.OpenStandardOutput();
                await this.RunLanguageServer(input, output);
                await input.DisposeAsync();
                await output.DisposeAsync();
                break;
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
                Version = AppMeta.GetVersion().ToString()
            })
            .OnInitialize(async (server, request, token) => await this.InitializeWorkspace([.. request.WorkspaceFolders ?? []]))
            .WithServices(services => services.AddSingleton(this))
            //.WithHandler<WorkspaceFoldersHandler>()
            //.WithHandler<WorkspaceSymbolsHandler>()
            //.WithHandler<DeletedFileHandler>()
            //.WithHandler<CreatedFileHandler>()
            //.WithHandler<TextDocumentSyncHandler>()
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
}