using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Handles DidCreateFile updates.
/// </summary>
/// <param name="workspaceOrchestrator">
/// The current workspace orchestrator.
/// </param>
public sealed class CreatedFileHandler([NotNull] WorkspaceOrchestrator workspaceOrchestrator) : DidCreateFileHandlerBase
{
    /// <summary>
    /// Handles a did create file request.
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
    public override async Task<Unit> Handle([NotNull] DidCreateFileParams request, CancellationToken cancellationToken)
    {
        await workspaceOrchestrator.CreateDocument(request.Files.Select(file => file.Uri), cancellationToken);
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
    /// The new did create file registration options.
    /// </returns>
    protected override DidCreateFileRegistrationOptions CreateRegistrationOptions(FileOperationsWorkspaceClientCapabilities capability, ClientCapabilities clientCapabilities)
    {
        return new DidCreateFileRegistrationOptions()
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