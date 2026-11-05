using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Semantics;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Serves compiler-resolved parameter names for ordinary Sushi source documents.
/// </summary>
/// <param name="workspace">The project-aware source workspace.</param>
public sealed class ParameterInlayHintHandler(WorkspaceOrchestrator workspace) : InlayHintsHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return new InlayHintContainer();
        }

        ParameterInlayHintMode mode = this.workspace.ParameterInlayHintMode;
        if (mode is ParameterInlayHintMode.Off)
        {
            return new InlayHintContainer();
        }

        SourceSemanticAnalysisSet? analysisSet = await this.workspace.GetParameterInlayHintAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null)
        {
            return new InlayHintContainer();
        }

        SourceAnalysisResult analysis = analysisSet.Selected;
        int rangeStart;
        int rangeEnd;
        try
        {
            rangeStart = analysis.Snapshot.GetByteOffset(request.Range.Start.Line, request.Range.Start.Character);
            rangeEnd = analysis.Snapshot.GetByteOffset(request.Range.End.Line, request.Range.End.Character);
        }
        catch (ArgumentException)
        {
            return new InlayHintContainer();
        }
        catch (InvalidOperationException)
        {
            return new InlayHintContainer();
        }

        List<InlayHint> hints = [];
        foreach (BoundParameterHint hint in analysis.ParameterHints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(hint.ArgumentSpan.Snapshot, analysis.Snapshot)
                || hint.IsNamedArgument
                || mode is ParameterInlayHintMode.Selective && hint.IsDirectIdentifierMatch
                || hint.ArgumentSpan.Start < rangeStart
                || hint.ArgumentSpan.Start >= rangeEnd)
            {
                continue;
            }

            (int line, int character) = analysis.Snapshot.GetUtf16Position(hint.ArgumentSpan.Start);
            hints.Add(new InlayHint
            {
                Position = new Position(line, character),
                Label = hint.Parameter.Name + ":",
                Kind = InlayHintKind.Parameter,
                PaddingRight = true
            });
        }

        if (!await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return new InlayHintContainer();
        }

        return new InlayHintContainer(hints);
    }

    /// <inheritdoc />
    protected override InlayHintRegistrationOptions CreateRegistrationOptions(InlayHintClientCapabilities capability, ClientCapabilities clientCapabilities)
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
