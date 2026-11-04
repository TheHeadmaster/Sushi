using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Source;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides standard Go to Definition for compiler-resolved ordinary Sushi source symbols.
/// </summary>
/// <param name="workspace">The project-aware source workspace.</param>
public sealed class SourceDefinitionHandler(WorkspaceOrchestrator workspace) : DefinitionHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
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

        SourceSpan? definition = SourceDefinitionResolver.Resolve(analysis, offset, cancellationToken);
        if (definition is not SourceSpan target || !await this.workspace.IsCurrentSourceAnalysisAsync(analysis, cancellationToken))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        (int startLine, int startCharacter) = target.Snapshot.GetUtf16Position(target.Start);
        (int endLine, int endCharacter) = target.Snapshot.GetUtf16Position(target.End);
        return new LocationOrLocationLinks(new Location
        {
            Uri = DocumentUri.FromFileSystemPath(target.Snapshot.Uri.LocalPath),
            Range = new LSPRange(startLine, startCharacter, endLine, endCharacter)
        });
    }

    /// <inheritdoc />
    protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
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
