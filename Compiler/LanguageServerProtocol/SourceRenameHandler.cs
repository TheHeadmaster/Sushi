using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Documentation;
using Sushi.Semantics;
using Sushi.Source;
using LSPRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Provides compiler-validated Rename and Prepare Rename for ordinary Sushi source symbols.
/// </summary>
/// <param name="workspace">The current project-aware source workspace.</param>
public sealed class SourceRenameHandler(WorkspaceOrchestrator workspace) : RenameHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    private bool supportsDocumentChanges;

    /// <inheritdoc />
    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Uri documentUri = request.TextDocument.Uri.ToUri();
        if (!IsSupportedSource(documentUri)
            || !SourceRenameIdentifier.TryValidate(request.NewName, out string replacementName, cancellationToken)
            || IsBuiltInTypeName(replacementName))
        {
            return null;
        }

        SourceSemanticAnalysisSet? analysisSet = await this.workspace.GetSourceReferenceAnalysisSet(documentUri, cancellationToken);
        if (analysisSet is null || !TryGetSelectedOccurrence(analysisSet.Selected, request.Position, out SourceSemanticOccurrence? selected))
        {
            return null;
        }

        if (selected is null || !SourceSemanticSymbolIdentity.TryCreate(selected.Symbol, out _))
        {
            return null;
        }

        IReadOnlyList<SourceSpan> references = SourceReferencesResolver.Resolve(analysisSet.Analyses,
            analysisSet.Selected, GetOffset(analysisSet.Selected.Snapshot, request.Position), includeDeclaration: true, cancellationToken);
        if (references.Count == 0)
        {
            return null;
        }

        List<SourceRenameTextEdit> edits = [];
        foreach (SourceSpan reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasSemanticSpelling(reference, selected.Symbol.Name, cancellationToken))
            {
                return null;
            }

            edits.Add(new SourceRenameTextEdit(reference, request.NewName));
        }

        if (selected.Symbol.Kind is SourceSemanticSymbolKind.Parameter
            && !TryAddParameterDocumentationEdits(selected.Symbol, request.NewName, edits))
        {
            return null;
        }

        edits.Sort((left, right) =>
        {
            int pathComparison = PathComparer.Compare(Path.GetFullPath(left.Span.Snapshot.Uri.LocalPath),
                Path.GetFullPath(right.Span.Snapshot.Uri.LocalPath));
            return pathComparison != 0 ? pathComparison : left.Span.Start.CompareTo(right.Span.Start);
        });

        if (!TryValidateEditSet(edits, cancellationToken))
        {
            return null;
        }

        bool isNoOp = edits.All(edit => GetText(edit.Span) == edit.NewText);
        if (!isNoOp && !await SourceRenameValidation.IsSemanticallySafeAsync(analysisSet, selected, references, edits, cancellationToken))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken))
        {
            return null;
        }

        if (isNoOp)
        {
            return new WorkspaceEdit();
        }

        WorkspaceEdit workspaceEdit = this.CreateWorkspaceEdit(edits, analysisSet, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await this.workspace.IsCurrentSourceAnalysisSetAsync(analysisSet, cancellationToken) ? workspaceEdit : null;
    }

    /// <inheritdoc />
    protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
    {
        this.supportsDocumentChanges = clientCapabilities.Workspace?.WorkspaceEdit?.DocumentChanges == true;
        return new RenameRegistrationOptions
        {
            PrepareProvider = true,
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Language = "sushi",
                Scheme = "file",
                Pattern = "**/*.sus"
            })
        };
    }

    private WorkspaceEdit CreateWorkspaceEdit(IReadOnlyList<SourceRenameTextEdit> edits, SourceSemanticAnalysisSet analysisSet,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<SourceRenameTextEdit>> grouped = new(PathComparer);
        foreach (SourceRenameTextEdit edit in edits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(edit.Span.Snapshot.Uri.LocalPath);
            if (!grouped.TryGetValue(path, out List<SourceRenameTextEdit>? group))
            {
                group = [];
                grouped.Add(path, group);
            }

            group.Add(edit);
        }

        List<KeyValuePair<DocumentUri, IEnumerable<TextEdit>>> changes = [];
        List<WorkspaceEditDocumentChange> documentChanges = [];
        foreach ((string path, List<SourceRenameTextEdit> fileEdits) in grouped.OrderBy(item => item.Key, PathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileEdits.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
            List<TextEdit> textEdits = [];
            foreach (SourceRenameTextEdit edit in fileEdits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int startLine, int startCharacter) = edit.Span.Snapshot.GetUtf16Position(edit.Span.Start);
                (int endLine, int endCharacter) = edit.Span.Snapshot.GetUtf16Position(edit.Span.End);
                textEdits.Add(new TextEdit
                {
                    Range = new LSPRange(startLine, startCharacter, endLine, endCharacter),
                    NewText = edit.NewText
                });
            }

            DocumentUri uri = DocumentUri.FromFileSystemPath(path);
            changes.Add(new KeyValuePair<DocumentUri, IEnumerable<TextEdit>>(uri, textEdits));
            if (this.supportsDocumentChanges)
            {
                SourceSnapshot snapshot = fileEdits[0].Span.Snapshot;
                int? version = analysisSet.OpenDocumentUris.Any(openUri => openUri.IsSamePath(snapshot.Uri))
                    ? snapshot.Version
                    : null;
                documentChanges.Add(new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = version },
                    Edits = textEdits
                });
            }
        }

        return this.supportsDocumentChanges
            ? new WorkspaceEdit { DocumentChanges = new Container<WorkspaceEditDocumentChange>(documentChanges) }
            : new WorkspaceEdit { Changes = changes.ToDictionary(pair => pair.Key, pair => pair.Value) };
    }

    private static bool TryAddParameterDocumentationEdits(SourceSemanticSymbol symbol, string replacement, List<SourceRenameTextEdit> edits)
    {
        DocumentationBlock? documentation = symbol.Documentation;
        if (documentation is null)
        {
            return true;
        }

        if (!documentation.IsAttached || symbol.DefinitionSpan is not SourceSpan definition
            || !ReferenceEquals(documentation.Snapshot, definition.Snapshot)
            || !ReferenceEquals(documentation.Declaration, symbol.Declaration))
        {
            return false;
        }

        foreach (DocumentationDirective directive in documentation.Content.Directives)
        {
            if (directive.Kind is not DocumentationDirectiveKind.Parameter
                || !StringComparer.Ordinal.Equals(directive.ParameterName, symbol.Name))
            {
                continue;
            }

            if (directive.ParameterNameSpan is not SourceSpan parameterName
                || !ReferenceEquals(parameterName.Snapshot, documentation.Snapshot)
                || parameterName.Start < documentation.Span.Start || parameterName.End > documentation.Span.End
                || !StringComparer.Ordinal.Equals(GetSemanticName(parameterName), symbol.Name))
            {
                return false;
            }

            edits.Add(new SourceRenameTextEdit(parameterName, replacement));
        }

        return true;
    }

    private static bool TryValidateEditSet(IReadOnlyList<SourceRenameTextEdit> edits, CancellationToken cancellationToken)
    {
        HashSet<string> paths = new(PathComparer);
        foreach (IGrouping<string, SourceRenameTextEdit> file in edits.GroupBy(edit => Path.GetFullPath(edit.Span.Snapshot.Uri.LocalPath), PathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceRenameTextEdit[] ordered = [.. file.OrderBy(edit => edit.Span.Start).ThenBy(edit => edit.Span.End)];
            int previousEnd = -1;
            foreach (SourceRenameTextEdit edit in ordered)
            {
                if (edit.Span.Start < previousEnd || edit.Span.Start >= edit.Span.End || edit.Span.End > edit.Span.Snapshot.SourceLength
                    || !edit.Span.Snapshot.IsValidUtf8 || !HasExactLspBoundaries(edit.Span)
                    || !paths.Add($"{file.Key}:{edit.Span.Start}:{edit.Span.End}"))
                {
                    return false;
                }

                previousEnd = edit.Span.End;
            }
        }

        return edits.Count > 0;
    }

    private static bool TryGetSelectedOccurrence(SourceAnalysisResult analysis, Position position, out SourceSemanticOccurrence? occurrence)
    {
        occurrence = null;
        try
        {
            int offset = analysis.Snapshot.GetByteOffset(position.Line, position.Character);
            occurrence = SourceSemanticOccurrenceLookup.FindOccurrence(analysis.SemanticOccurrences, analysis.Snapshot, offset);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static int GetOffset(SourceSnapshot snapshot, Position position)
        => snapshot.GetByteOffset(position.Line, position.Character);

    private static string GetText(SourceSpan span) => Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);

    private static string GetSemanticName(SourceSpan span)
    {
        string spelling = GetText(span);
        return spelling.StartsWith('@') ? spelling[1..] : spelling;
    }

    private static bool HasSemanticSpelling(SourceSpan span, string expectedName, CancellationToken cancellationToken)
        => SourceRenameIdentifier.TryValidate(GetText(span), out string semanticName, cancellationToken)
            && StringComparer.Ordinal.Equals(semanticName, expectedName);

    private static bool HasExactLspBoundaries(SourceSpan span)
    {
        try
        {
            (int startLine, int startCharacter) = span.Snapshot.GetUtf16Position(span.Start);
            (int endLine, int endCharacter) = span.Snapshot.GetUtf16Position(span.End);
            return span.Snapshot.GetByteOffset(startLine, startCharacter) == span.Start
                && span.Snapshot.GetByteOffset(endLine, endCharacter) == span.End;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsSupportedSource(Uri uri)
        => uri.IsFile && Path.GetExtension(uri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuiltInTypeName(string name)
        => name is "bool" or "void" or "int8" or "uint8" or "int16" or "uint16" or "int32" or "uint32"
            or "int64" or "uint64" or "int128" or "uint128";

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

/// <summary>
/// Confirms that a source cursor selects a supported compiler-resolved Rename target.
/// </summary>
/// <param name="workspace">The current project-aware source workspace.</param>
public sealed class SourcePrepareRenameHandler(WorkspaceOrchestrator workspace) : PrepareRenameHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Uri uri = request.TextDocument.Uri.ToUri();
        if (!uri.IsFile || !Path.GetExtension(uri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SourceAnalysisResult? analysis = await this.workspace.GetSourceHoverAnalysis(uri, cancellationToken);
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

        SourceSemanticOccurrence? occurrence = SourceSemanticOccurrenceLookup.FindOccurrence(analysis.SemanticOccurrences, analysis.Snapshot, offset);
        if (occurrence is null || !SourceSemanticSymbolIdentity.TryCreate(occurrence.Symbol, out _)
            || !IsValidIdentifierSpan(occurrence.Span, occurrence.Symbol.Name, cancellationToken))
        {
            return null;
        }

        if (!await this.workspace.IsCurrentSourceAnalysisAsync(analysis, cancellationToken))
        {
            return null;
        }

        SourceSpan span = occurrence.Span;
        (int startLine, int startCharacter) = span.Snapshot.GetUtf16Position(span.Start);
        (int endLine, int endCharacter) = span.Snapshot.GetUtf16Position(span.End);
        LSPRange range = new(startLine, startCharacter, endLine, endCharacter);
        string placeholder = Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
        return new RangeOrPlaceholderRange(new PlaceholderRange { Range = range, Placeholder = placeholder });
    }

    /// <inheritdoc />
    protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            PrepareProvider = true,
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter
            {
                Language = "sushi",
                Scheme = "file",
                Pattern = "**/*.sus"
            })
        };

    private static bool IsValidIdentifierSpan(SourceSpan span, string expectedName, CancellationToken cancellationToken)
    {
        string spelling = Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
        return SourceRenameIdentifier.TryValidate(spelling, out string semanticName, cancellationToken)
            && StringComparer.Ordinal.Equals(semanticName, expectedName);
    }
}
