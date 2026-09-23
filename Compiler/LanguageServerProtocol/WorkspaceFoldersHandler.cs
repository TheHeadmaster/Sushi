using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Handles DidChangeWorkspaceFolders requests.
/// </summary>
/// <param name="workspaceOrchestrator">
/// The current workspace orchestrator.
/// </param>
public sealed class WorkspaceFoldersHandler([NotNull] WorkspaceOrchestrator workspaceOrchestrator) : DidChangeWorkspaceFoldersHandlerBase
{
    /// <summary>
    /// Handles a DidChangeWorkspaceFolders request.
    /// </summary>
    /// <param name="request">
    /// The request to handle.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public override async Task<Unit> Handle([NotNull] DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken)
    {
        await workspaceOrchestrator.UpdateWorkspaceFolders(request.Event.Added, request.Event.Removed, cancellationToken);

        return new Unit();
    }

    /// <summary>
    /// Creates the registration options for the handler.
    /// </summary>
    /// <param name="capability">
    /// The capability.
    /// </param>
    /// <param name="clientCapabilities">
    /// The capabilities supported by the client.
    /// </param>
    /// <returns>
    /// The new did change workspace folder registration options.
    /// </returns>
    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities)
    {
        return new DidChangeWorkspaceFolderRegistrationOptions()
        {
            Supported = true,
            ChangeNotifications = true
        };
    }
}