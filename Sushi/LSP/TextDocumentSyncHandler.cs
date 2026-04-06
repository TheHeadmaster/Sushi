using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Serilog;
using Sushi.Diagnostics;
using Builder = System.Collections.Immutable.ImmutableArray<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>.Builder;

namespace Sushi.LSP;

public sealed class TextDocumentSyncHandler(ILanguageServerFacade facade, SushiLanguageService sushi) : TextDocumentSyncHandlerBase
{
    private List<DocumentUri> trackedDocuments = [];

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "sushi");

    public override async Task<Unit> Handle([NotNull] DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        DocumentUri? existing = this.trackedDocuments.FirstOrDefault(x => x.Path == request.TextDocument.Uri.Path);

        if (existing is not null)
        {
            this.trackedDocuments.Remove(existing);
        }

        this.trackedDocuments.Add(request.TextDocument.Uri);
        await this.Update(request.TextDocument.Uri, request.TextDocument.Version, null);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        await this.Update(request.TextDocument.Uri, request.TextDocument.Version, request.ContentChanges.First().Text);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        await this.Update(request.TextDocument.Uri, null, null);
        return new Unit();
    }

    public override async Task<Unit> Handle([NotNull] DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        await this.Update(request.TextDocument.Uri, null, null);
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

    private async Task Update([NotNull] DocumentUri textDocumentUri, int? version, string? text)
    {
        if (!File.Exists(textDocumentUri.Path))
        {
            this.trackedDocuments.Remove(textDocumentUri);
        }

        List<CompilerMessage> messages = !string.IsNullOrWhiteSpace(text) ? await sushi.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath) : await sushi.UpdateSource(textDocumentUri.ToUri().AbsolutePath);

        await this.PublishDiagnosticsForDocument(version, messages, textDocumentUri);

        foreach (DocumentUri document in this.trackedDocuments.Where(x => Uri.Compare(x.ToUri(), textDocumentUri.ToUri(), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) != 0))
        {
            messages = await sushi.UpdateSource(document.ToUri().AbsolutePath);
            await this.PublishDiagnosticsForDocument(version, messages, document);
        }
    }

    private async Task PublishDiagnosticsForDocument(int? version, List<CompilerMessage> messages, DocumentUri document)
    {
        if (!File.Exists(document.Path))
        {
            this.trackedDocuments.Remove(document);
        }

        Builder diagnostics = ImmutableArray<Diagnostic>.Empty.ToBuilder();

        foreach (CompilerMessage message in messages)
        {
            diagnostics.Add(await message.ToDiagnostic());
        }

        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams()
        {
            Diagnostics = new Container<Diagnostic>(diagnostics.ToArray()),
            Uri = document,
            Version = version
        });
    }
}
