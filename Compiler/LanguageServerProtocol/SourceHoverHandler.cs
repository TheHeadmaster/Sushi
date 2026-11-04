using OmniSharp.Extensions.LanguageServer.Protocol;
using Sushi.Analysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides standard LSP Hover for compiler-resolved ordinary Sushi source symbols.
/// </summary>
/// <param name="workspace">The source workspace and its current semantic snapshots.</param>
public sealed class SourceHoverHandler(WorkspaceOrchestrator workspace) : HoverHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SourceAnalysisResult? analysis = await this.workspace.GetSourceHoverAnalysis(documentUri, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        int offset;
        try
        {
            offset = analysis.Snapshot.GetByteOffset(request.Position.Line, request.Position.Character);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        SourceHoverResult? result = SourceHoverResolver.Resolve(analysis, offset, cancellationToken);
        if (result is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        (int startLine, int startCharacter) = result.Range.Snapshot.GetUtf16Position(result.Range.Start);
        (int endLine, int endCharacter) = result.Range.Snapshot.GetUtf16Position(result.Range.End);
        return new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = result.Markdown
            }),
            Range = new LSPRange(startLine, startCharacter, endLine, endCharacter)
        };
    }

    /// <inheritdoc />
    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Language = "sushi",
                Scheme = "file",
                Pattern = "**/*.sus"
            })
        };
}
