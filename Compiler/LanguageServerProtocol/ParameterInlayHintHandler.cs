using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

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
            Location? parameterLocation = CreateParameterLocation(hint);
            StringOrInlayHintLabelParts label = parameterLocation is null
                ? new StringOrInlayHintLabelParts(hint.Parameter.Name + ":")
                : new StringOrInlayHintLabelParts(new[]
                {
                    new InlayHintLabelPart
                    {
                        Value = hint.Parameter.Name + ":",
                        Tooltip = new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = SourceHoverResolver.FormatParameterTooltip(hint.Parameter, hint.Callable, cancellationToken)
                        },
                        Location = parameterLocation
                    }
                });
            hints.Add(new InlayHint
            {
                Position = new Position(line, character),
                Label = label,
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

    private static Location? CreateParameterLocation(BoundParameterHint hint)
    {
        if (hint.Callable.Declaration is not FunctionDeclarationSyntax declaration
            || hint.Parameter.Index < 0
            || hint.Parameter.Index >= declaration.Parameters.Count
            || hint.Parameter.Index >= hint.Callable.Function.Parameters.Count
            || hint.Callable.Function.Parameters[hint.Parameter.Index] != hint.Parameter)
        {
            return null;
        }

        ParameterSyntax parameter = declaration.Parameters[hint.Parameter.Index];
        SyntaxToken nameToken = parameter.NameToken;
        SourceSpan definition = nameToken.Span;
        SourceSnapshot snapshot = definition.Snapshot;
        if (!nameToken.IsSourceBacked
            || !snapshot.IsValidUtf8
            || !ReferenceEquals(snapshot, declaration.Span.Snapshot)
            || !StringComparer.Ordinal.Equals(IdentifierBinder.Bind(nameToken), hint.Parameter.Name)
            || definition.Start < 0
            || definition.Start >= definition.End
            || definition.End > snapshot.SourceLength)
        {
            return null;
        }

        try
        {
            (int startLine, int startCharacter) = snapshot.GetUtf16Position(definition.Start);
            (int endLine, int endCharacter) = snapshot.GetUtf16Position(definition.End);
            return new Location
            {
                Uri = DocumentUri.FromFileSystemPath(snapshot.Uri.LocalPath),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(startLine, startCharacter, endLine, endCharacter)
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public override Task<InlayHint> Handle(InlayHint request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(request);
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
