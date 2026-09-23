using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Handles DidDeleteFile updates.
/// </summary>
/// <param name="workspaceOrchestrator">
/// The current workspace orchestrator.
/// </param>
public sealed class DeletedFileHandler([NotNull] WorkspaceOrchestrator workspaceOrchestrator) : DidDeleteFileHandlerBase
{
    /// <summary>
    /// Handles a did delete file request.
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
    public override async Task<Unit> Handle([NotNull] DidDeleteFileParams request, CancellationToken cancellationToken)
    {
        await workspaceOrchestrator.DeleteDocument(request.Files.Select(file => file.Uri), cancellationToken);
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
    /// The new did delete file registration options.
    /// </returns>
    protected override DidDeleteFileRegistrationOptions CreateRegistrationOptions(FileOperationsWorkspaceClientCapabilities capability, ClientCapabilities clientCapabilities)
    {
        return new DidDeleteFileRegistrationOptions()
        {
            Filters = new Container<FileOperationFilter>(
                new FileOperationFilter()
                {
                    Pattern = new FileOperationPattern()
                    {
                        Glob = "**/*.{sus,susproj,susln}",
                        Matches = FileOperationPatternKind.File
                    }
                }
            )
        };
    }
}