using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Handles watched filesystem changes.
/// </summary>
/// <param name="workspaceOrchestrator">
/// The current workspace orchestrator.
/// </param>
public sealed class WatchedFilesHandler([NotNull] WorkspaceOrchestrator workspaceOrchestrator) : DidChangeWatchedFilesHandlerBase
{
    /// <summary>
    /// Handles a DidChangeWatchedFiles request.
    /// </summary>
    /// <param name="request">
    /// The request.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public override async Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
    {
        await workspaceOrchestrator.ApplyFileChanges(request.Changes, cancellationToken);

        return Unit.Value;
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
    /// The new did change watched files registration options.
    /// </returns>
    protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DidChangeWatchedFilesRegistrationOptions()
        {
            Watchers = new Container<FileSystemWatcher>(new FileSystemWatcher
            {
                GlobPattern = "**/*.{sus,susproj,susln}"!,
                Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete
            })
        };
    }
}