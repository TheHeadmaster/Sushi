using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Coordinates declaration analysis and selected-function binding without executable compilation or editor dependencies.
/// </summary>
public sealed class CompilerInspectionService : IDisposable
{
    private readonly SourceAnalyzer sourceAnalyzer = new();
    private readonly CompilerInspectionCache cache;

    /// <summary>
    /// Creates an inspection service with a bounded cache for this service lifetime.
    /// </summary>
    public CompilerInspectionService()
        : this(new CompilerInspectionCache())
    {
    }

    /// <summary>
    /// Creates an inspection service with a bounded cache for deterministic capacity tests.
    /// </summary>
    /// <param name="cacheCapacity">The maximum number of exact function input identities to retain.</param>
    internal static CompilerInspectionService CreateForTesting(int cacheCapacity)
    {
        return new CompilerInspectionService(new CompilerInspectionCache(cacheCapacity));
    }

    private CompilerInspectionService(CompilerInspectionCache cache) => this.cache = cache;

    /// <summary>
    /// Provides cache state to focused compiler inspection tests without exposing performance counters publicly.
    /// </summary>
    internal CompilerInspectionCache Cache => this.cache;

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
        string? functionName = null;
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

        functionName = selected.Callable.Name;
        IReadOnlyList<PackageCallable> visible = ProjectCallableCatalog.SelectVisible(selected.Source, catalog, cancellationToken);
        CompilerInspectionCacheKey cacheKey = CompilerInspectionCache.CreateKey(projectPath, projectSnapshot, snapshots, selected.Callable.Symbol, selected.Declaration.Span);
        InspectionDocument? document = this.GetOrGenerateDocument(cacheKey, selected.Callable.Symbol, selected.Declaration, visible,
            snapshots, request.Stage, diagnostics, cancellationToken);
        if (diagnostics.HasErrors() || document is null)
        {
            return Complete(selected.Declaration.Span, null);
        }

        return Complete(selected.Declaration.Span, document);

        CompilerInspectionResult Complete(SourceSpan? declaration, InspectionDocument? document)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompilerInspectionContext context = new(projectPath, projectSnapshot, snapshots, definitions);
            CompilerInspectionResult result = new(request.Stage, context, diagnostics.ReportDiagnostics(), declaration, document, functionName);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    /// <summary>
    /// Generates one function representation from an already captured compilation context.
    /// </summary>
    /// <param name="context">The immutable project configuration and source snapshots to use.</param>
    /// <param name="functionSymbol">The compiler-generated identity assigned in that context.</param>
    /// <param name="stage">The representation stage retained by the originating artifact.</param>
    /// <param name="cancellationToken">Cancels parsing, signature reconstruction, binding, and formatting.</param>
    /// <returns>A representation or diagnostics for the requested function, retaining the original context.</returns>
    /// <exception cref="InvalidOperationException">The retained context cannot reproduce its callable catalog.</exception>
    public async Task<CompilerInspectionResult> InspectFunctionAsync(CompilerInspectionContext context, string functionSymbol,
        InspectionStage stage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        if (stage is not (InspectionStage.StructuredIR or InspectionStage.LoweredIR))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        cancellationToken.ThrowIfCancellationRequested();
        IDiagnosticReporter diagnostics = new DiagnosticReporter();
        ProjectAnalysisResult projectAnalysis = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(context.ProjectSnapshot, diagnostics, cancellationToken);
        if (diagnostics.HasErrors() || projectAnalysis.Project is null)
        {
            throw new InvalidOperationException("The retained project configuration cannot be analyzed for navigation.");
        }

        List<SourceAnalysisResult> sources = [];
        foreach (SourceSnapshot snapshot in context.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceAnalysisResult source = (SourceAnalysisResult)await this.sourceAnalyzer.AnalyzeDeclarations(snapshot, diagnostics, cancellationToken);
            sources.Add(source);
        }

        IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect(sources, diagnostics, cancellationToken);
        if (diagnostics.HasErrors() || catalog.Count != context.Functions.Count)
        {
            throw new InvalidOperationException("The retained source snapshots cannot reproduce the originating callable catalog.");
        }

        foreach (ProjectCallable callable in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!context.Functions.TryGetValue(callable.Callable.Symbol, out SourceSpan retained)
                || !ReferenceEquals(retained.Snapshot, callable.Declaration.Span.Snapshot)
                || retained.Start != callable.Declaration.Span.Start || retained.End != callable.Declaration.Span.End)
            {
                throw new InvalidOperationException("The retained source snapshots do not match the originating callable identities.");
            }
        }

        if (!context.Functions.TryGetValue(functionSymbol, out SourceSpan targetDeclaration))
        {
            throw new InvalidOperationException("The requested compiler-generated function identity is not present in the retained context.");
        }

        ProjectCallable? selected = catalog.FirstOrDefault(item => ReferenceEquals(item.Declaration.Span.Snapshot, targetDeclaration.Snapshot)
            && item.Declaration.Span.Start == targetDeclaration.Start && item.Declaration.Span.End == targetDeclaration.End);
        if (selected is null || !StringComparer.Ordinal.Equals(selected.Callable.Symbol, functionSymbol))
        {
            throw new InvalidOperationException("The requested compiler-generated function identity does not match its retained declaration.");
        }

        IReadOnlyList<PackageCallable> visible = ProjectCallableCatalog.SelectVisible(selected.Source, catalog, cancellationToken);
        CompilerInspectionCacheKey cacheKey = CompilerInspectionCache.CreateKey(context.ProjectPath, context.ProjectSnapshot, context.Sources,
            selected.Callable.Symbol, selected.Declaration.Span);
        InspectionDocument? document = this.GetOrGenerateDocument(cacheKey, selected.Callable.Symbol, selected.Declaration, visible,
            context.Sources, stage, diagnostics, cancellationToken);
        if (diagnostics.HasErrors() || document is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new CompilerInspectionResult(stage, context, diagnostics.ReportDiagnostics(), selected.Declaration.Span, null, selected.Callable.Name);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new CompilerInspectionResult(stage, context, diagnostics.ReportDiagnostics(), selected.Declaration.Span, document, selected.Callable.Name);
    }

    /// <summary>
    /// Reuses a complete stage product or performs only the missing conversion and formatting work.
    /// </summary>
    private InspectionDocument? GetOrGenerateDocument(CompilerInspectionCacheKey cacheKey, string functionSymbol, FunctionDeclarationSyntax declaration,
        IReadOnlyList<PackageCallable> visibleCallables, IReadOnlyList<SourceSnapshot> sources, InspectionStage stage,
        IDiagnosticReporter diagnostics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (this.cache.TryGet(cacheKey, out CompilerInspectionCacheEntry cached))
        {
            ReplayBindingDiagnostics(cached.BindingDiagnostics, sources, diagnostics);
            InspectionDocument? cachedDocument = stage == InspectionStage.StructuredIR ? cached.StructuredDocument : cached.LoweredDocument;
            if (cachedDocument is not null)
            {
                return cachedDocument;
            }

            if (stage is InspectionStage.StructuredIR && cached.StructuredIR is not null)
            {
                InspectionDocument document = StructuredIRFormatter.Format(cached.StructuredIR, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                this.cache.StoreStructuredDocument(cacheKey, document);
                return document;
            }

            if (stage is InspectionStage.LoweredIR && cached.LoweredIR is not null)
            {
                InspectionDocument document = LoweredIRFormatter.Format(cached.LoweredIR, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                this.cache.StoreLoweredDocument(cacheKey, document);
                return document;
            }

            if (stage is InspectionStage.LoweredIR && cached.StructuredIR is not null)
            {
                LoweredFunction lowered = IRFunctionLowerer.Lower(cached.StructuredIR, cancellationToken);
                InspectionDocument document = LoweredIRFormatter.Format(lowered, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                this.cache.StoreLowered(cacheKey, cached.StructuredIR, lowered, document, cached.BindingDiagnostics);
                return document;
            }
        }

        DiagnosticReporter bindingDiagnostics = new();
        FunctionBindResult bound = FunctionDeclarationBinder.Bind(declaration, bindingDiagnostics, visibleCallables, cancellationToken);
        if (bound.Function is null && !bindingDiagnostics.HasErrors())
        {
            bindingDiagnostics.GenerateError(ErrorType.UnsupportedMVPExpression, declaration.Span);
        }

        if (bindingDiagnostics.HasErrors() || bound.Function is null)
        {
            diagnostics.CommitReporter(bindingDiagnostics);
            return null;
        }

        BoundFunction function = bound.Function;
        BoundFunction identified = new(function.Accessibility, functionSymbol, function.ReturnType, function.Parameters, function.Body);
        IRFunction structured = BoundFunctionIRConverter.Convert(identified, cancellationToken);
        IReadOnlyList<CachedInspectionDiagnostic> retainedDiagnostics = CacheDiagnostics(bindingDiagnostics.ReportDiagnostics());
        if (stage is InspectionStage.StructuredIR)
        {
            InspectionDocument document = StructuredIRFormatter.Format(structured, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            this.cache.StoreStructured(cacheKey, structured, document, retainedDiagnostics);
            diagnostics.CommitReporter(bindingDiagnostics);
            return document;
        }

        LoweredFunction loweredFunction = IRFunctionLowerer.Lower(structured, cancellationToken);
        InspectionDocument loweredDocument = LoweredIRFormatter.Format(loweredFunction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        this.cache.StoreLowered(cacheKey, structured, loweredFunction, loweredDocument, retainedDiagnostics);
        diagnostics.CommitReporter(bindingDiagnostics);
        return loweredDocument;
    }

    private static IReadOnlyList<CachedInspectionDiagnostic> CacheDiagnostics(IReadOnlyList<SushiDiagnostic> diagnostics)
    {
        CachedInspectionDiagnostic[] cached = [.. diagnostics.Select(diagnostic => new CachedInspectionDiagnostic(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Severity,
            Path.GetFullPath(diagnostic.Span.Snapshot.Uri.LocalPath),
            diagnostic.Span.Start,
            diagnostic.Span.End))];
        return Array.AsReadOnly(cached);
    }

    private static void ReplayBindingDiagnostics(IReadOnlyList<CachedInspectionDiagnostic> cachedDiagnostics,
        IReadOnlyList<SourceSnapshot> sources, IDiagnosticReporter diagnostics)
    {
        StringComparer pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        DiagnosticReporter replayed = new();
        foreach (CachedInspectionDiagnostic diagnostic in cachedDiagnostics)
        {
            SourceSnapshot? snapshot = sources.FirstOrDefault(source => pathComparer.Equals(
                Path.GetFullPath(source.Uri.LocalPath), diagnostic.SourcePath));
            if (snapshot is null)
            {
                throw new InvalidOperationException("A cached binding diagnostic does not belong to the current inspection context.");
            }

            SourceSpan span = new(snapshot, diagnostic.Start, diagnostic.End);
            if (diagnostic.Severity is DiagnosticSeverity.Error)
            {
                replayed.GenerateErrorWithCustomMessage((ErrorType)diagnostic.Code, diagnostic.Message, span);
            }
            else if (diagnostic.Severity is DiagnosticSeverity.Warning)
            {
                replayed.GenerateWarningWithCustomMessage((WarningType)diagnostic.Code, diagnostic.Message, span);
            }
            else
            {
                throw new InvalidOperationException("The cached binding diagnostic has an unsupported severity.");
            }
        }

        diagnostics.CommitReporter(replayed);
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

    /// <summary>
    /// Releases compiler representations retained by this service.
    /// </summary>
    public void Dispose() => this.cache.Dispose();
}
