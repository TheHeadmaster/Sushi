using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Sushi.LSP;

/// <summary>
/// Handles text document sync updates.
/// </summary>
/// <param name="facade">
/// The language server facade to use to send responses.
/// </param>
/// <param name="sushi">
/// The sushi language service.
/// </param>
public sealed class TextDocumentSyncHandler([NotNull] ILanguageServerFacade facade, [NotNull] SushiLanguageService sushi) : TextDocumentSyncHandlerBase
{
    /// <summary>
    /// Gets text document attributes for a specific document uri.
    /// </summary>
    /// <param name="uri">
    /// The document uri.
    /// </param>
    /// <returns>
    /// The new text document attributes.
    /// </returns>
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "sushi");

    /// <summary>
    /// Handles an open text document request.
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
    public override async Task<Unit> Handle([NotNull] DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, request.TextDocument.Version, null);
        return new Unit();
    }

    /// <summary>
    /// Handles a change text document request.
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
    public override async Task<Unit> Handle([NotNull] DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, request.TextDocument.Version, request.ContentChanges.First().Text);
        return new Unit();
    }

    /// <summary>
    /// Handles a save text document request.
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
    public override async Task<Unit> Handle([NotNull] DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, null, null);
        return new Unit();
    }

    /// <summary>
    /// Handles a close text document request.
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
    public override async Task<Unit> Handle([NotNull] DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        await sushi.UpdateDocument(facade, request.TextDocument.Uri, null, null);
        return Unit.Value;
    }

    /// <summary>
    /// Creates the registration options for the handler.
    /// </summary>
    /// <param name="capability">
    /// The text synchronization capability.
    /// </param>
    /// <param name="clientCapabilities">
    /// The capabilities supported by the client.
    /// </param>
    /// <returns>
    /// The new text document registration options.
    /// </returns>
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