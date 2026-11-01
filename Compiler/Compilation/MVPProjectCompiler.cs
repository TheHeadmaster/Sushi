
using Microsoft.Extensions.FileSystemGlobbing;
using Sushi.Analysis;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Parsing.Syntax;
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

        string projectPath = ResolveProjectPath(projectOrFolderPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project \"{projectPath}\" has no containing directory.");

        SourceSnapshot projectSnapshot = await LoadSnapshot(projectPath, cancellationToken);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        ProjectAnalysisResult projectResult = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(
            projectSnapshot, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors())
        {
            return Failed(diagnosticReporter);
        }

        ProjectDefinition project = projectResult.Project
            ?? throw new InvalidOperationException("Project analysis succeeded without producing a project definition.");

        IReadOnlyList<string> sourcePaths = DiscoverSourcePaths(projectDirectory, project);

        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException($"Project \"{project.Name}\" did not select any Sushi source files.");
        }

        List<SourceAnalysisResult> sources = [];

        foreach (string sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceSnapshot snapshot = await LoadSnapshot(sourcePath, cancellationToken);
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

        IReadOnlyList<ProjectCallable> catalog = CollectProjectCallables(sources, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors())
        {
            return Failed(diagnosticReporter);
        }

        SourceAnalysisResult rootSource = sources.Single(source => ReferenceEquals(source.Snapshot, root.Declaration.Span.Snapshot));
        IReadOnlyList<PackageCallable> rootCallables = SelectVisibleCallables(rootSource, catalog);

        LoweredFunction? entry = BindAndLowerRootLifecycleMethod(
            root.Entry, "__sushi_root_entry", rootCallables, diagnosticReporter, cancellationToken);

        LoweredFunction? stop = root.Stop is null
            ? null
            : BindAndLowerRootLifecycleMethod(root.Stop, "__sushi_root_stop", rootCallables, diagnosticReporter, cancellationToken);

        List<LoweredFunction> packageFunctions = [];

        foreach (ProjectCallable item in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<PackageCallable> visible = SelectVisibleCallables(item.Source, catalog);
            FunctionBindResult result = FunctionDeclarationBinder.Bind(item.Declaration, diagnosticReporter, visible, cancellationToken);

            if (result.Function is null)
            {
                continue;
            }

            if (!BelongsToSameScope(item.Source, rootSource)
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

    private static IReadOnlyList<ProjectCallable> CollectProjectCallables(
        IReadOnlyList<SourceAnalysisResult> sources,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        List<ProjectCallable> catalog = [];

        foreach (SourceAnalysisResult source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceFileSyntax syntax = (SourceFileSyntax)source.SyntaxTree.Root;

            if (source.Functions.Count != syntax.FunctionDeclarations.Count)
            {
                diagnosticReporter.GenerateErrorWithCustomMessage(
                    ErrorType.UnsupportedMVPExpression,
                    "A project function has a signature unsupported by the current executable subset.",
                    syntax.Span);

                continue;
            }

            for (int i = 0; i < syntax.FunctionDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BoundFunction signature = source.Functions[i];
                FunctionDeclarationSyntax declaration = syntax.FunctionDeclarations[i];
                string symbol = $"__sushi_package_function_{catalog.Count}";

                catalog.Add(new ProjectCallable(source, declaration, new PackageCallable(signature, symbol)));
            }
        }

        return catalog;
    }

    private static IReadOnlyList<PackageCallable> SelectVisibleCallables(SourceAnalysisResult source, IReadOnlyList<ProjectCallable> catalog)
        => [.. catalog
            .Where(item => BelongsToSameScope(item.Source, source))
            .Where(item => item.Callable.Function.Accessibility is not (Accessibility.Private or Accessibility.Protected))
            .Select(item => item.Callable)];

    private static bool BelongsToSameScope(SourceAnalysisResult left, SourceAnalysisResult right)
        => left.Package is not null
            && right.Package is not null
            && left.Namespace is not null
            && right.Namespace is not null
            && StringComparer.Ordinal.Equals(left.Package.QualifiedName, right.Package.QualifiedName)
            && StringComparer.Ordinal.Equals(left.Namespace.QualifiedName, right.Namespace.QualifiedName);

    private static string ResolveProjectPath(string projectOrFolderPath)
    {
        string path = Path.GetFullPath(projectOrFolderPath);

        if (File.Exists(path))
        {
            string extension = Path.GetExtension(path);

            if (extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (extension.Equals(".susln", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Solution compilation is not supported by the executable MVP. Compile a .susproj directly.");
            }

            throw new ArgumentException($"\"{path}\" is not a Sushi project file.", nameof(projectOrFolderPath));
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Project directory \"{path}\" does not exist.");
        }

        string[] projects = [.. Directory
            .EnumerateFiles(path, "*.susproj", SearchOption.TopDirectoryOnly)
            .OrderBy(project => project, StringComparer.Ordinal)];

        return projects.Length switch
        {
            1 => projects[0],
            0 => throw new InvalidOperationException($"Directory \"{path}\" does not contain a Sushi project file."),
            _ => throw new InvalidOperationException($"Directory \"{path}\" contains multiple Sushi project files. Specify the project to compile explicitly.")
        };
    }

    private static IReadOnlyList<string> DiscoverSourcePaths(string projectDirectory, ProjectDefinition project)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Matcher matcher = new(comparison);

        foreach (string pattern in project.Build.Sources)
        {
            matcher.AddInclude(pattern);
        }

        foreach (string pattern in project.Source.Exclude)
        {
            matcher.AddExclude(pattern);
        }

        return
        [
            .. matcher
                .GetResultsInFullPath(projectDirectory)
                .Where(path => Path.GetExtension(path).Equals(".sus", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
        ];
    }

    private static string GetMVPOutputPath(string projectDirectory, ProjectDefinition project)
    {
        string target = project.Build.DefaultTarget
            ?? throw new InvalidOperationException("The analyzed project does not have a default build target.");

        return Path.Combine(projectDirectory, "bin", target, project.Assembly);
    }

    private static async Task<SourceSnapshot> LoadSnapshot(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(path);
        byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        return SourceSnapshot.FromUtf8(new Uri(fullPath), version: null, bytes);
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

    private sealed record ProjectCallable(SourceAnalysisResult Source, FunctionDeclarationSyntax Declaration, PackageCallable Callable);
}