using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Semantics;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides parameter signature information while editing calls to
/// uniquely identified package-level Sushi functions.
/// </summary>
/// <param name="workspace">The project-aware source workspace.</param>
public sealed class SourceSignatureHelpHandler(WorkspaceOrchestrator workspace) : SignatureHelpHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SourceSemanticAnalysisSet? analysisSet = await this.workspace.GetParameterInlayHintAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null)
        {
            return null;
        }

        SourceAnalysisResult analysis = analysisSet.Selected;
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

        SourceSignatureHelpResult? resolved = SourceSignatureHelpResolver.Resolve(analysis, offset, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        SignatureHelp result = CreateSignatureHelp(resolved, cancellationToken);

        if (!await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return null;
        }

        return result;
    }

    private static SignatureHelp CreateSignatureHelp(SourceSignatureHelpResult resolved, CancellationToken cancellationToken)
    {
        PackageCallable callable = resolved.Callable;
        BoundFunction function = callable.Function;

        ParameterInformation[] parameters = [.. function.Parameters.Select(parameter =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new ParameterInformation
            {
                Label = $"{GetTypeName(parameter.Type)} {parameter.Name}",
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = SourceHoverResolver.FormatParameterTooltip(parameter, callable, cancellationToken)
                })
            };
        })];

        string parameterList = string.Join(", ", parameters.Select(parameter => parameter.Label.ToString()));
        SignatureInformation signature = new()
        {
            Label = $"{GetTypeName(function.ReturnType)} {function.Name}({parameterList})",
            Parameters = new Container<ParameterInformation>(parameters)
        };

        return new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature),
            ActiveSignature = 0,
            ActiveParameter = resolved.ActiveParameter
        };
    }

    private static string GetTypeName(BoundType type) => type switch
    {
        BoundType.Int32 => "int32",
        BoundType.Bool => "bool",
        BoundType.Void => "void",
        _ => "unknown"
    };

    /// <inheritdoc />
    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability,
        ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Language = "sushi",
                Scheme = "file",
                Pattern = "**/*.sus"
            }),
            TriggerCharacters = new Container<string>("(", ","),
            RetriggerCharacters = new Container<string>(",")
        };
}