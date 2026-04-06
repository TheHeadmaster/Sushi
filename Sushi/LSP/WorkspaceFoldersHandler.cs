using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LSP;

public sealed class WorkspaceFoldersHandler(SushiLanguageService sushi) : DidChangeWorkspaceFoldersHandlerBase
{
    public override async Task<Unit> Handle([NotNull] DidChangeWorkspaceFoldersParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateProjectFolders([.. request.Event.Added], [.. request.Event.Removed]);

        return Unit.Value;
    }

    protected override DidChangeWorkspaceFolderRegistrationOptions CreateRegistrationOptions(ClientCapabilities clientCapabilities)
    {
        return new DidChangeWorkspaceFolderRegistrationOptions()
        {
            Supported = true,
            ChangeNotifications = true
        };
    }
}
