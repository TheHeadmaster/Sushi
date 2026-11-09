using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides project-aware package-function completion for ordinary Sushi
/// source documents using compiler-established callable visibility.
/// </summary>
/// <param name="workspace">
/// The workspace supplying current source snapshots and semantic analysis.
/// </param>
public sealed class SourceCompletionHandler(WorkspaceOrchestrator workspace) : CompletionHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!documentUri.IsFile || !Path.GetExtension(documentUri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return Empty();
        }

        SourceSemanticAnalysisSet? analysisSet = await this.workspace.GetParameterInlayHintAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null)
        {
            return Empty();
        }

        SourceAnalysisResult analysis = analysisSet.Selected;
        int offset;

        try
        {
            offset = analysis.Snapshot.GetByteOffset(request.Position.Line, request.Position.Character);
        }
        catch (ArgumentException)
        {
            return Empty();
        }
        catch (InvalidOperationException)
        {
            return Empty();
        }

        SourceCompletionResult? resolved = SourceCompletionResolver.Resolve(analysis, offset, cancellationToken);
        if (resolved is null)
        {
            return Empty();
        }

        CompletionList completion = CreateCompletionList(resolved, cancellationToken);

        if (!await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return Empty();
        }

        return completion;
    }

    /// <inheritdoc />
    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(request);
    }

    private static CompletionList CreateCompletionList(SourceCompletionResult resolved, CancellationToken cancellationToken)
    {
        SourceSnapshot snapshot = resolved.Replacement.Snapshot;
        (int startLine, int startCharacter) = snapshot.GetUtf16Position(resolved.Replacement.Start);
        (int endLine, int endCharacter) = snapshot.GetUtf16Position(resolved.Replacement.End);

        LSPRange replacement = new(startLine, startCharacter, endLine, endCharacter);
        List<CompletionItem> items = [];

        foreach (PackageCallable callable in resolved.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string spelling = GetSourceSpelling(callable);
            items.Add(new CompletionItem
            {
                Label = spelling,
                Kind = CompletionItemKind.Function,
                Detail = FormatSignature(callable.Function),
                FilterText = callable.Name,
                SortText = callable.Name,
                TextEdit = new TextEdit
                {
                    Range = replacement,
                    NewText = spelling
                }
            });
        }

        return new CompletionList(items, isIncomplete: false);
    }

    private static string GetSourceSpelling(PackageCallable callable)
        => callable.Declaration.Name.Type is SyntaxType.EscapedIdentifierToken
            ? "@" + callable.Name
            : callable.Name;

    private static string FormatSignature(BoundFunction function)
    {
        string parameters = string.Join(", ", function.Parameters.Select(parameter =>
            $"{GetTypeName(parameter.Type)} {parameter.Name}"));

        return $"{GetTypeName(function.ReturnType)} {function.Name}({parameters})";
    }

    private static string GetTypeName(BoundType type) => type switch
    {
        BoundType.Int32 => "int32",
        BoundType.Bool => "bool",
        BoundType.Void => "void",
        _ => "unknown"
    };

    private static CompletionList Empty() => new(Array.Empty<CompletionItem>(), isIncomplete: false);

    /// <inheritdoc />
    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Language = "sushi",
                Scheme = "file",
                Pattern = "**/*.sus"
            }),
            ResolveProvider = false
        };
}