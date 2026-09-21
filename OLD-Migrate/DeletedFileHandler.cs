using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LSP;

/// <summary>
/// Handler for DidDeleteFile requests.
/// </summary>
/// <param name="facade">
/// The language server facade.
/// </param>
/// <param name="sushi">
/// The sushi language service.
/// </param>
public sealed class DeletedFileHandler([NotNull] ILanguageServerFacade facade, [NotNull] SushiLanguageService sushi) : DidDeleteFileHandlerBase
{
    /// <summary>
    /// Handles the DidDeleteFile request.
    /// </summary>
    /// <param name="request">
    /// The request.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the operation.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task."/>
    /// </returns>
    public override async Task<Unit> Handle([NotNull] DidDeleteFileParams request, CancellationToken cancellationToken)
    {
        await sushi.RemoveDocuments(facade, [.. request.Files.Select(x => x.Uri)]);
        return Unit.Value;
    }

    /// <summary>
    /// Creates the registration options for this request.
    /// </summary>
    /// <param name="capability">
    /// The capability for FileOperationsWorkspace.
    /// </param>
    /// <param name="clientCapabilities">
    /// The client capabilities.
    /// </param>
    /// <returns>
    /// The new options.
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
                        Glob = "**/*.sus",
                        Matches = FileOperationPatternKind.File
                    }
                })
        };
    }
}
