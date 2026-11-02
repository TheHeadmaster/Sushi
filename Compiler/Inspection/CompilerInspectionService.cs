using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Coordinates declaration analysis and selected-function binding without executable compilation or editor dependencies.
/// </summary>
public sealed class CompilerInspectionService
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    /// <summary>
    /// Inspects one package function using the supplied source snapshots and existing compiler transitions.
    /// Only the selected function body is bound; all other functions contribute signatures only.
    /// </summary>
    /// <param name="request">
    /// Project identity, authoritative source overrides, UTF-8 selection, and requested stage.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels loading, analysis, binding, conversion, lowering, and formatting.
    /// </param>
    /// <returns>
    /// A complete representation or a failed result containing source diagnostics and analysis context.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A snapshot is duplicated, is not a discovered project source, or the selection is outside package functions.
    /// </exception>
    public async Task<CompilerInspectionResult> InspectAsync(CompilerInspectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, SourceSnapshot> overrides = CollectOverrides(request, cancellationToken);
        string projectPath = ProjectSourceLoader.ResolveProjectPath(request.ProjectOrFolderPath, cancellationToken);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project \"{projectPath}\" has no containing directory.");
        SourceSnapshot projectSnapshot = await ProjectSourceLoader.LoadSnapshot(projectPath, cancellationToken);
        IDiagnosticReporter diagnostics = new DiagnosticReporter();
        ProjectAnalysisResult analysis = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(projectSnapshot, diagnostics, cancellationToken);
        List<SourceSnapshot> snapshots = [];
        Dictionary<string, SourceSpan> definitions = new(StringComparer.Ordinal);
        if (diagnostics.HasErrors())
        {
            return Complete(null, null);
        }

        ProjectDefinition project = analysis.Project
            ?? throw new InvalidOperationException("Project analysis succeeded without producing a project definition.");
        IReadOnlyList<string> paths = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, project, cancellationToken);
        HashSet<string> discovered = new(paths, GetPathComparer());
        foreach (string path in overrides.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!discovered.Contains(path))
            {
                throw new ArgumentException($"Snapshot \"{path}\" is not a discovered project source file.", nameof(request));
            }
        }

        List<SourceAnalysisResult> sources = [];
        SourceAnalysisResult? selectedSource = null;
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceSnapshot snapshot = overrides.TryGetValue(path, out SourceSnapshot? supplied)
                ? supplied
                : await ProjectSourceLoader.LoadSnapshot(path, cancellationToken);
            snapshots.Add(snapshot);
            SourceAnalysisResult source = (SourceAnalysisResult)await this.sourceAnalyzer.AnalyzeDeclarations(snapshot, diagnostics, cancellationToken);
            sources.Add(source);
            if (ReferenceEquals(snapshot, request.Snapshot))
            {
                selectedSource = source;
            }
        }

        if (diagnostics.HasErrors())
        {
            return Complete(null, null);
        }

        IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect(sources, diagnostics, cancellationToken);
        if (diagnostics.HasErrors())
        {
            return Complete(null, null);
        }

        ProjectCallable? selected = null;
        foreach (ProjectCallable item in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceSpan span = item.Declaration.Span;
            definitions.Add(item.Callable.Symbol, span);
            if (ReferenceEquals(item.Source, selectedSource) && request.ByteOffset >= span.Start && request.ByteOffset < span.End)
            {
                selected = item;
            }
        }

        if (selected is null)
        {
            throw new ArgumentException("The selection must be within a supported package-level function declaration or body.", nameof(request));
        }

        IReadOnlyList<PackageCallable> visible = ProjectCallableCatalog.SelectVisible(selected.Source, catalog, cancellationToken);
        FunctionBindResult bound = FunctionDeclarationBinder.Bind(selected.Declaration, diagnostics, visible, cancellationToken);
        if (bound.Function is null && !diagnostics.HasErrors())
        {
            diagnostics.GenerateError(ErrorType.UnsupportedMVPExpression, selected.Declaration.Span);
        }

        if (diagnostics.HasErrors() || bound.Function is null)
        {
            return Complete(selected.Declaration.Span, null);
        }

        BoundFunction function = bound.Function;
        BoundFunction identified = new(function.Accessibility, selected.Callable.Symbol, function.ReturnType, function.Parameters, function.Body);
        IRFunction structured = BoundFunctionIRConverter.Convert(identified, cancellationToken);
        InspectionDocument document = request.Stage switch
        {
            InspectionStage.StructuredIR => StructuredIRFormatter.Format(structured, cancellationToken),
            InspectionStage.LoweredIR => LoweredIRFormatter.Format(IRFunctionLowerer.Lower(structured, cancellationToken), cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        return Complete(selected.Declaration.Span, document);

        CompilerInspectionResult Complete(SourceSpan? declaration, InspectionDocument? document)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompilerInspectionContext context = new(projectPath, projectSnapshot, snapshots, definitions);
            CompilerInspectionResult result = new(request.Stage, context, diagnostics.ReportDiagnostics(), declaration, document);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    private static Dictionary<string, SourceSnapshot> CollectOverrides(CompilerInspectionRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, SourceSnapshot> snapshots = new(GetPathComparer());
        Add(request.Snapshot);
        foreach (SourceSnapshot snapshot in request.AdditionalSnapshots)
        {
            Add(snapshot);
        }

        return snapshots;

        void Add(SourceSnapshot snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!snapshot.Uri.IsAbsoluteUri || !snapshot.Uri.IsFile || snapshot.Uri.Query.Length != 0 || snapshot.Uri.Fragment.Length != 0)
            {
                throw new ArgumentException("Inspection snapshots must identify absolute local file URIs without queries or fragments.", nameof(request));
            }

            string path = Path.GetFullPath(snapshot.Uri.LocalPath);
            if (!snapshots.TryAdd(path, snapshot))
            {
                throw new ArgumentException($"Duplicate inspection snapshot for \"{path}\".", nameof(request));
            }
        }
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}