using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Handles text document sync updates.
/// </summary>
/// <param name="workspaceOrchestrator">
/// The current workspace orchestrator.
/// </param>
public sealed class TextDocumentSyncHandler([NotNull] WorkspaceOrchestrator workspaceOrchestrator) : TextDocumentSyncHandlerBase
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
    public override TextDocumentAttributes GetTextDocumentAttributes([NotNull] DocumentUri uri)
    {
        string extension = Path.GetExtension(uri.Path);

        return extension switch
        {
            ".susproj" => new(uri, "susproj"),
            ".susln" => new(uri, "susln"),
            ".sus" => new(uri, "sushi"),
            _ => throw new InvalidOperationException("Unrecognized text document source file")
        };
    }

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
        await workspaceOrchestrator.OpenDocument(
            request.TextDocument.Uri.ToUri(),
            request.TextDocument.Version,
            request.TextDocument.Text,
            cancellationToken);
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
        await workspaceOrchestrator.ChangeDocument(
            request.TextDocument.Uri.ToUri(),
            request.TextDocument.Version,
            request.ContentChanges.Single().Text,
            cancellationToken);
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
        await workspaceOrchestrator.SaveDocument(
            request.TextDocument.Uri.ToUri(),
            cancellationToken);
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
        await workspaceOrchestrator.CloseDocument(request.TextDocument.Uri.ToUri(), cancellationToken);
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
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter() { Pattern = "**/*.sus" },
                new TextDocumentFilter() { Pattern = "**/*.susproj" },
                new TextDocumentFilter() { Pattern = "**/*.susln" }),
            Change = TextDocumentSyncKind.Full,
            Save = true
        };
    }
}