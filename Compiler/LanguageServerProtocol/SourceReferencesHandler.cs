using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Source;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides standard source Find All References for compiler-resolved Sushi symbols.
/// </summary>
/// <param name="workspace">
/// The current project-aware source workspace.
/// </param>
public sealed class SourceReferencesHandler(WorkspaceOrchestrator workspace) : ReferencesHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SourceSemanticAnalysisSet? analysisSet = await this.workspace.GetSourceReferenceAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null)
        {
            return null;
        }

        int offset;
        try
        {
            offset = analysisSet.Selected.Snapshot.GetByteOffset(request.Position.Line, request.Position.Character);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        IReadOnlyList<SourceSpan> references = SourceReferencesResolver.Resolve(analysisSet.Analyses,
            analysisSet.Selected, offset, request.Context.IncludeDeclaration, cancellationToken);
        List<Location> locations = new(references.Count);
        foreach (SourceSpan span in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (int startLine, int startCharacter) = span.Snapshot.GetUtf16Position(span.Start);
            (int endLine, int endCharacter) = span.Snapshot.GetUtf16Position(span.End);
            locations.Add(new Location
            {
                Uri = DocumentUri.FromFileSystemPath(span.Snapshot.Uri.LocalPath),
                Range = new LSPRange(startLine, startCharacter, endLine, endCharacter)
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return null;
        }

        return new LocationContainer(locations);
    }

    /// <inheritdoc />
    protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
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
