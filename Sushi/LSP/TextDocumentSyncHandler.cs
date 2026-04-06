using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Builder = System.Collections.Immutable.ImmutableArray<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>.Builder;
using Sushi.Diagnostics;
using Serilog;

namespace Sushi.LSP;

public sealed class TextDocumentSyncHandler(ILanguageServerFacade facade, SushiLanguageService sushi) : TextDocumentSyncHandlerBase
{

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "sushi");

    public override async Task<Unit> Handle([NotNull] DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
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
        List<CompilerMessage> messages = !string.IsNullOrWhiteSpace(text) ? await sushi.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath) : await sushi.UpdateSource(textDocumentUri.ToUri().AbsolutePath);

        // Parse your stuff here

        // Diagnostics are sent a document at a time, this example is for demonstration purposes only
        Builder diagnostics = ImmutableArray<Diagnostic>.Empty.ToBuilder();

        foreach (CompilerMessage message in messages)
        {
            diagnostics.Add(await message.ToDiagnostic());
        }

        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams()
        {
            Diagnostics = new Container<Diagnostic>(diagnostics.ToArray()),
            Uri = textDocumentUri,
            Version = version
        });
    }
}
