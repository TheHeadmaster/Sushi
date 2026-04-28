using System;
using System.Collections.Generic;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LSP;

public sealed class SemanticTokenHandler(SushiLanguageService sushi) : SemanticTokensHandlerBase
{
    private static readonly SemanticTokensLegend legend = new()
    {
        TokenTypes = new Container<SemanticTokenType>([SemanticTokenType.TypeParameter]),
        TokenModifiers = []
    };

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter() { Pattern = "**/*.sus" }),
            Full = true,
            Legend = legend
        };
    }
    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken) => Task.FromResult(new SemanticTokensDocument(legend));
    protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken) => await sushi.PushSemanticTokens(builder, identifier.TextDocument.Uri, legend);
}
