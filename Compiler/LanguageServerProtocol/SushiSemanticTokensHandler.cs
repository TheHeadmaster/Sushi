
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides full-document semantic highlighting for Sushi source files.
/// TextMate remains responsible for lexical highlighting outside classified
/// source-backed symbols.
/// </summary>
/// <param name="workspace">
/// The authoritative source of current editor snapshots and parsed syntax.
/// </param>
public sealed class SushiSemanticTokensHandler(WorkspaceOrchestrator workspace) : SemanticTokensHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identifier);

        Uri documentUri = identifier.TextDocument.Uri.ToUri();
        SourceFileSyntax? syntax = await this.workspace.GetSemanticSyntax(documentUri, cancellationToken);

        if (syntax is null)
        {
            return;
        }

        foreach (SourceSemanticToken token in SourceSemanticTokenClassifier.Classify(syntax, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceSpan span = token.Span;
            (int line, int character) = span.Snapshot.GetUtf16Position(span.Start);
            (int endLine, int endCharacter) = span.Snapshot.GetUtf16Position(span.End);

            if (line != endLine || endCharacter <= character)
            {
                continue;
            }

            int length = endCharacter - character;

            if (token.IsDeclaration)
            {
                builder.Push(line, character, length, token.TokenType, SemanticTokenModifier.Declaration);
            }
            else
            {
                builder.Push(line, character, length, token.TokenType, Array.Empty<SemanticTokenModifier>());
            }
        }
    }

    /// <inheritdoc />
    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SemanticTokensDocument(this.RegistrationOptions.Legend));
    }

    /// <inheritdoc />
    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("sushi"),
            Legend = new SemanticTokensLegend(),
            Full = new SemanticTokensCapabilityRequestFull { Delta = false },
            Range = false
        };
}
