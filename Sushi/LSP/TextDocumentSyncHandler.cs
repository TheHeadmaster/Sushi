using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Sushi.Diagnostics;

namespace Sushi.LSP;

public sealed class TextDocumentSyncHandler(ILanguageServerFacade facade, SushiLanguageService sushi) : TextDocumentSyncHandlerBase
{
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "sushi");

    public override async Task<Unit> Handle([NotNull] DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, request.TextDocument.Version, null);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, request.TextDocument.Version, request.ContentChanges.First().Text);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, null, null);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, null, null);
        return Unit.Value;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter() { Pattern = "**/*.sus" }),
            Change = TextDocumentSyncKind.Full,
            Save = true
        };
    }
}
