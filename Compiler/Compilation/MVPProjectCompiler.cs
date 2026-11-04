
using Sushi.Analysis;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Compilation;

/// <summary>
/// Compiles executable Sushi projects through signature collection, function
/// body binding, root lifecycle resolution, lowering, and native emission.
/// </summary>
public sealed class MVPProjectCompiler(MVPExecutableBuilder executableBuilder)
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    /// <summary>
    /// Creates a project compiler using the current native executable pipeline.
    /// </summary>
    internal MVPProjectCompiler() : this(new MVPExecutableBuilder())
    {
    }

    /// <summary>
    /// Compiles a project into a native executable using its declared root activity.
    /// </summary>
    /// <param name="projectOrFolderPath">
    /// A .susproj path or directory containing exactly one Sushi project file.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels compilation and native linking.
    /// </param>
    /// <returns>
    /// The resulting executable and all static diagnostics.
    /// </returns>
    public async Task<MVPCompilationResult> CompileAsync(string projectOrFolderPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectOrFolderPath);
        cancellationToken.ThrowIfCancellationRequested();

        string projectPath = ProjectSourceLoader.ResolveProjectPath(projectOrFolderPath, cancellationToken);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project \"{projectPath}\" has no containing directory.");

        SourceSnapshot projectSnapshot = await ProjectSourceLoader.LoadSnapshot(projectPath, cancellationToken);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ProjectAnalysisResult projectResult = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(
            projectSnapshot, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors())
        {
            return Failed(diagnosticReporter);
        }

        ProjectDefinition project = projectResult.Project
            ?? throw new InvalidOperationException("Project analysis succeeded without producing a project definition.");

        IReadOnlyList<string> sourcePaths = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, project, cancellationToken);

        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException($"Project \"{project.Name}\" did not select any Sushi source files.");
        }

        List<SourceAnalysisResult> sources = [];

        foreach (string sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceSnapshot snapshot = await ProjectSourceLoader.LoadSnapshot(sourcePath, cancellationToken);
            SourceAnalysisResult source = (SourceAnalysisResult)await this.sourceAnalyzer.AnalyzeDeclarations(
                snapshot, diagnosticReporter, cancellationToken);

            sources.Add(source);
        }

        if (diagnosticReporter.HasErrors())
        {
            return Failed(diagnosticReporter);
        }

        BoundActivity? root = RootActivityAnalyzer.SelectRoot(
            sources, projectSnapshot, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors() || root is null)
        {
            return Failed(diagnosticReporter);
        }

        if (root.Lifetime is not ActivityLifetime.Task)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedRootActivityLifetime, root.Declaration.LifetimeKeyword.Span);
            return Failed(diagnosticReporter);
        }

        IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect(sources, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors())
        {
            return Failed(diagnosticReporter);
        }

        foreach (SourceAnalysisResult source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectDiagnosticAnalyzer.AnalyzeDocumentationReferences(source, catalog, diagnosticReporter, cancellationToken);
        }

        SourceAnalysisResult rootSource = sources.Single(source => ReferenceEquals(source.Snapshot, root.Declaration.Span.Snapshot));
        IReadOnlyList<PackageCallable> rootCallables = ProjectCallableCatalog.SelectVisible(rootSource, catalog, cancellationToken);

        LoweredFunction? entry = BindAndLowerRootLifecycleMethod(
            root.Entry, "__sushi_root_entry", rootCallables, diagnosticReporter, cancellationToken);

        LoweredFunction? stop = root.Stop is null
            ? null
            : BindAndLowerRootLifecycleMethod(root.Stop, "__sushi_root_stop", rootCallables, diagnosticReporter, cancellationToken);

        List<LoweredFunction> packageFunctions = [];

        foreach (ProjectCallable item in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<PackageCallable> visible = ProjectCallableCatalog.SelectVisible(item.Source, catalog, cancellationToken);
            FunctionBindResult result = FunctionDeclarationBinder.Bind(item.Declaration, diagnosticReporter, visible, cancellationToken);

            if (result.Function is null)
            {
                continue;
            }

            if (!ProjectCallableCatalog.BelongsToSameScope(item.Source, rootSource)
                || item.Callable.Function.Accessibility is Accessibility.Private or Accessibility.Protected)
            {
                continue;
            }

            BoundFunction function = result.Function;
            BoundFunction nativeFunction = new(function.Accessibility, item.Callable.Symbol, function.ReturnType, function.Parameters, function.Body);
            IRFunction structured = BoundFunctionIRConverter.Convert(nativeFunction, cancellationToken);

            packageFunctions.Add(IRFunctionLowerer.Lower(structured, cancellationToken));
        }

        if (diagnosticReporter.HasErrors() || entry is null || (root.Stop is not null && stop is null))
        {
            return Failed(diagnosticReporter);
        }

        string outputPath = GetMVPOutputPath(projectDirectory, project);

        NativeExecutableArtifact executable = await executableBuilder.BuildRootActivityAsync(
            entry, stop, packageFunctions, outputPath, cancellationToken);

        return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable);
    }

    private static MVPCompilationResult Failed(IDiagnosticReporter diagnosticReporter)
        => new(diagnosticReporter.ReportDiagnostics(), executable: null);

    private static string GetMVPOutputPath(string projectDirectory, ProjectDefinition project)
    {
        string target = project.Build.DefaultTarget
            ?? throw new InvalidOperationException("The analyzed project does not have a default build target.");

        return Path.Combine(projectDirectory, "bin", target, project.Assembly);
    }

    /// <summary>
    /// Binds one supported root lifecycle body with the project-level callables
    /// visible in its source package and namespace.
    /// </summary>
    /// <param name="method">
    /// The validated lifecycle method declaration.
    /// </param>
    /// <param name="symbol">
    /// The compiler-owned native symbol.
    /// </param>
    /// <param name="callables">
    /// The visible package callable signatures.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding and lowering.
    /// </param>
    /// <returns>
    /// A lowered lifecycle method, or null on failure.
    /// </returns>
    private static LoweredFunction? BindAndLowerRootLifecycleMethod(
        BoundActivityMethod method,
        string symbol,
        IReadOnlyList<PackageCallable> callables,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(callables);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (method.Declaration.Parameters.Count != 0)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedRootLifecycleParameters, method.Declaration.OpenParenthesisToken.Span);
            return null;
        }

        BoundBlock? body = FunctionDeclarationBinder.BindBody(
            method.Declaration.Body,
            BoundType.Void,
            [],
            diagnosticReporter,
            cancellationToken,
            callables);

        if (body is null)
        {
            return null;
        }

        BoundFunction function = new(method.Accessibility, symbol, BoundType.Void, body);
        IRFunction structured = BoundFunctionIRConverter.Convert(function, cancellationToken);

        return IRFunctionLowerer.Lower(structured, cancellationToken);
    }
}