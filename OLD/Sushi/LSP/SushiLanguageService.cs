using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.LSP;

public sealed class SushiLanguageService
{
    private readonly Parser parser = new();
    private AbstractSyntaxTree tree = null!;

    public async Task UpdateSource([NotNull] string sourceFilePath)
    {
        TokenFile file = await Lexer.LexFile(sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => Uri.Compare(new Uri(x.FilePath), new Uri(sourceFilePath), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

        if (existingIndex != -1)
        {

            this.tokenFiles[existingIndex] = file;
        }
    }

    public async Task UpdateSourceText([NotNull] string text, string sourceFilePath)
    {
        TokenFile file = await Lexer.LexStringAsFileText(text, sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => Uri.Compare(new Uri(x.FilePath), new Uri(sourceFilePath), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

        if (existingIndex != -1)
        {
            this.tokenFiles[existingIndex] = file;
        }
    }
    private async Task UpdateSyntaxTree()
    {
        Parser parser = new();
        this.tree = await parser.ParseSource(this.tokenFiles);
    }

    public async Task UpdateDocument([NotNull] ILanguageServerFacade facade, [NotNull] DocumentUri textDocumentUri, int? version, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            await this.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath);
        }
        else
        {
            await this.UpdateSource(textDocumentUri.ToUri().AbsolutePath);
        }

        await this.UpdateSyntaxTree();

        await this.PublishDiagnosticsForAllDocuments(facade, version);
    }



    public async Task PushSemanticTokens([NotNull] SemanticTokensBuilder builder, [NotNull] DocumentUri document, [NotNull] SemanticTokensLegend legend) => await new SemanticTokenVisitor(builder, document, legend).Visit(this.tree);
}
