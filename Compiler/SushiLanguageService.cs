using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Sushi.LanguageServerProtocol;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi code bases.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class SushiLanguageService
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    /// <summary>
    /// Handles the orchestration and plumbing of diagnostics publishing and source document tracking.
    /// </summary>
    private readonly WorkspaceOrchestrator workspace = new();

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
            .OnInitialize((server, request, token) => this.workspace.Initialize([.. request.WorkspaceFolders ?? []], token))
            .OnStarted((server, token) => this.workspace.Start(server, token))
            .WithServices(services =>
            {
                services.AddSingleton(this);
                services.AddSingleton(this.workspace);
            })
            .WithHandler<WorkspaceFoldersHandler>()
            .WithHandler<WatchedFilesHandler>()
            .WithHandler<TextDocumentSyncHandler>()
        );

        await server.WaitForExit;

        this.workspace.Dispose();
    }

    /// <summary>
    /// Runs the language server with a <see cref="NetworkStream"/>, while set up to listen on localhost.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
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