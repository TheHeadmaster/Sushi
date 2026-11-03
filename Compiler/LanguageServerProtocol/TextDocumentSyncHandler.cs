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
        Uri documentUri = uri.ToUri();

        if (!IsManagedDocument(documentUri))
        {
            return new(uri, "plaintext");
        }

        string extension = Path.GetExtension(documentUri.LocalPath);

        return extension.ToLowerInvariant() switch
        {
            ".susproj" => new(uri, "susproj"),
            ".susln" => new(uri, "susln"),
            ".sus" => new(uri, "sushi"),
            _ => new(uri, "plaintext")
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
        if (!IsManagedDocument(request.TextDocument.Uri.ToUri()))
        {
            return Unit.Value;
        }

        await workspaceOrchestrator.OpenDocument(
            request.TextDocument.Uri.ToUri(),
            request.TextDocument.Version,
            request.TextDocument.Text,
            cancellationToken);
        return Unit.Value;
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
        if (!IsManagedDocument(request.TextDocument.Uri.ToUri()))
        {
            return Unit.Value;
        }

        await workspaceOrchestrator.ChangeDocument(
            request.TextDocument.Uri.ToUri(),
            request.TextDocument.Version,
            request.ContentChanges.Single().Text,
            cancellationToken);
        return Unit.Value;
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
        if (!IsManagedDocument(request.TextDocument.Uri.ToUri()))
        {
            return Unit.Value;
        }

        await workspaceOrchestrator.SaveDocument(
            request.TextDocument.Uri.ToUri(),
            cancellationToken);
        return Unit.Value;
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
        if (!IsManagedDocument(request.TextDocument.Uri.ToUri()))
        {
            return Unit.Value;
        }

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
                new TextDocumentFilter() { Scheme = "file", Pattern = "**/*.sus" },
                new TextDocumentFilter() { Scheme = "file", Pattern = "**/*.susproj" },
                new TextDocumentFilter() { Scheme = "file", Pattern = "**/*.susln" }),
            Change = TextDocumentSyncKind.Full,
            Save = true
        };
    }

    private static bool IsManagedDocument(Uri uri)
    {
        if (!uri.IsFile)
        {
            return false;
        }

        string extension = Path.GetExtension(uri.LocalPath);
        return extension.Equals(".sus", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".susln", StringComparison.OrdinalIgnoreCase);
    }
}
