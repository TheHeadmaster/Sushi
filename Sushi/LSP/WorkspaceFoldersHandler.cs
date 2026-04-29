using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LSP;

/// <summary>
/// Handles DidChangeWorkspaceFolders requests.
/// </summary>
/// <param name="facade">
/// Represents the language server.
/// </param>
/// <param name="sushi">
/// The sushi language service.
/// </param>
public sealed class WorkspaceFoldersHandler([NotNull] ILanguageServerFacade facade, [NotNull] SushiLanguageService sushi) : DidChangeWorkspaceFoldersHandlerBase
{
    /// <summary>
    /// Handles a DidChangeWorkspaceFolders request.
    /// </summary>
    /// <param name="request">
    /// The request to handle.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task<Unit> Handle([NotNull] DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateWorkspaceFolders(facade, [.. request.Event.Added], [.. request.Event.Removed], cancellationToken);

        return Unit.Value;
    }

    /// <summary>
    /// Creates the registration options for this handler.
    /// </summary>
    /// <param name="clientCapabilities">
    /// The capabilities from the client.
    /// </param>
    /// <returns>
    /// The registration options.
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
