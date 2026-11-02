using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Compilation;

/// <summary>
/// Performs source diagnostics with the callable visibility of each applicable project.
/// </summary>
internal sealed class ProjectDiagnosticAnalyzer
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    /// <summary>
    /// Analyzes current workspace snapshots without invoking native code generation.
    /// </summary>
    /// <param name="snapshots">
    /// The authoritative snapshots currently held by the workspace.
    /// </param>
    /// <param name="affectedUris">
    /// Documents whose changes require dependent project contexts to be reanalyzed, or null for a full workspace pass.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels declaration analysis and function-body binding.
    /// </param>
    /// <returns>
    /// Results and diagnostics kept separate for each originating document snapshot.
    /// </returns>
    internal async Task<IReadOnlyList<ProjectDiagnosticResult>> AnalyzeAsync(
        IReadOnlyList<SourceSnapshot> snapshots,
        IReadOnlyList<Uri>? affectedUris,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        cancellationToken.ThrowIfCancellationRequested();

        List<SourceSnapshot> sourceSnapshots = [.. snapshots.Where(snapshot => IsExtension(snapshot, ".sus"))];
        List<ProjectDiagnosticResult> results = [];
        List<ProjectContext> projects = [];
        List<string> invalidProjectDirectories = [];
        bool analyzeEntireWorkspace = affectedUris is null || affectedUris.Any(uri =>
            Path.GetExtension(uri.LocalPath).Equals(".susproj", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(uri.LocalPath).Equals(".susln", StringComparison.OrdinalIgnoreCase)
            || (Path.GetExtension(uri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase) && !File.Exists(uri.LocalPath)));

        foreach (SourceSnapshot snapshot in snapshots.Where(item => IsExtension(item, ".susproj")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticReporter reporter = new();
            ProjectAnalysisResult result = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(snapshot, reporter, cancellationToken);
            if (analyzeEntireWorkspace || affectedUris!.Any(uri => uri.IsSamePath(snapshot.Uri)))
            {
                results.Add(new ProjectDiagnosticResult(result, reporter));
            }

            if (result.Project is null)
            {
                string? invalidDirectory = Path.GetDirectoryName(snapshot.Uri.LocalPath);
                if (invalidDirectory is not null)
                {
                    invalidProjectDirectories.Add(invalidDirectory);
                }

                continue;
            }

            string projectDirectory = Path.GetDirectoryName(snapshot.Uri.LocalPath)
                ?? throw new InvalidOperationException($"Project \"{snapshot.Uri.LocalPath}\" has no containing directory.");
            IReadOnlyList<string> paths = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, result.Project, cancellationToken);
            List<SourceSnapshot> members = [];
            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri sourceUri = new(path);
                SourceSnapshot? source = sourceSnapshots.FirstOrDefault(item => item.Uri.IsSamePath(sourceUri));
                if (source is null)
                {
                    source = await ProjectSourceLoader.LoadSnapshot(path, cancellationToken);
                    sourceSnapshots.Add(source);
                }

                members.Add(source);
            }

            if (members.Count > 0)
            {
                projects.Add(new ProjectContext(projectDirectory, Array.AsReadOnly(members.ToArray())));
            }
        }

        ProjectContext[] selectedProjects = [.. projects.Where(project => analyzeEntireWorkspace
            || affectedUris!.Any(uri => project.Members.Any(member => member.Uri.IsSamePath(uri))
                || IsWithinDirectory(uri, project.Directory)))];
        HashSet<SourceSnapshot> selectedSnapshots = new(ReferenceEqualityComparer.Instance);
        if (analyzeEntireWorkspace)
        {
            selectedSnapshots.UnionWith(sourceSnapshots);
        }
        else
        {
            foreach (ProjectContext project in selectedProjects)
            {
                selectedSnapshots.UnionWith(project.Members);
            }

            foreach (SourceSnapshot source in sourceSnapshots.Where(snapshot => IsExtension(snapshot, ".sus")
                && affectedUris!.Any(uri => uri.IsSamePath(snapshot.Uri))))
            {
                selectedSnapshots.Add(source);
            }
        }

        List<SourceState> sources = [];
        foreach (SourceSnapshot snapshot in selectedSnapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticReporter reporter = new();
            SourceAnalysisResult result = (SourceAnalysisResult)await this.sourceAnalyzer.AnalyzeDeclarations(snapshot, reporter, cancellationToken);
            sources.Add(new SourceState(result, reporter));
        }

        Dictionary<SourceSnapshot, SourceState> sourceStates = new(ReferenceEqualityComparer.Instance);
        foreach (SourceState source in sources)
        {
            sourceStates.Add(source.Result.Snapshot, source);
        }

        foreach (ProjectContext project in selectedProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceState[] members = [.. project.Members.Select(snapshot => sourceStates[snapshot])];
            if (members.Any(source => source.Reporter.HasErrors()))
            {
                continue;
            }

            IDiagnosticReporter catalogDiagnostics = new DiagnosticReporter();
            IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect(
                [.. members.Select(source => source.Result)], catalogDiagnostics, cancellationToken);
            if (catalogDiagnostics.HasErrors())
            {
                RouteCatalogDiagnostics(members, catalogDiagnostics.ReportDiagnostics());
                continue;
            }

            foreach (SourceState source in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CountContainingProjects(source, projects) != 1 || IsWithinInvalidProject(source.Result.Snapshot.Uri, invalidProjectDirectories))
                {
                    continue;
                }

                BindBodies(source, ProjectCallableCatalog.SelectVisible(source.Result, catalog, cancellationToken), cancellationToken);
            }
        }

        foreach (SourceState source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int membershipCount = CountContainingProjects(source, projects);
            if (membershipCount != 0 || source.Reporter.HasErrors()
                || IsWithinInvalidProject(source.Result.Snapshot.Uri, invalidProjectDirectories))
            {
                continue;
            }

            IDiagnosticReporter catalogDiagnostics = new DiagnosticReporter();
            IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect([source.Result], catalogDiagnostics, cancellationToken);
            if (catalogDiagnostics.HasErrors())
            {
                source.Reporter.CommitReporter(catalogDiagnostics);
                continue;
            }

            BindBodies(source, ProjectCallableCatalog.SelectVisible(source.Result, catalog, cancellationToken), cancellationToken);
        }

        foreach (SourceState source in sources)
        {
            results.Add(new ProjectDiagnosticResult(source.Result, source.Reporter));
        }

        return results;
    }

    private static void BindBodies(SourceState source, IReadOnlyList<PackageCallable> callables, CancellationToken cancellationToken)
    {
        SourceFileSyntax syntax = (SourceFileSyntax)source.Result.SyntaxTree.Root;
        List<BoundFunction> functions = [];
        foreach (FunctionDeclarationSyntax declaration in syntax.FunctionDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FunctionBindResult result = FunctionDeclarationBinder.Bind(declaration, source.Reporter, callables, cancellationToken);
            if (result.Function is not null)
            {
                functions.Add(result.Function);
            }
        }

        source.Result = source.Result with { Functions = functions };
    }

    private static int CountContainingProjects(SourceState source, IReadOnlyList<ProjectContext> projects)
        => projects.Count(project => project.Members.Any(member => ReferenceEquals(member, source.Result.Snapshot)));

    private static void RouteCatalogDiagnostics(IReadOnlyList<SourceState> sources, IReadOnlyList<SushiDiagnostic> diagnostics)
    {
        foreach (SushiDiagnostic diagnostic in diagnostics)
        {
            SourceState? source = sources.FirstOrDefault(item => ReferenceEquals(item.Result.Snapshot, diagnostic.Span.Snapshot));
            if (source is not null && diagnostic.Severity is DiagnosticSeverity.Error
                && Enum.IsDefined((ErrorType)diagnostic.Code))
            {
                source.Reporter.GenerateErrorWithCustomMessage((ErrorType)diagnostic.Code, diagnostic.Message, diagnostic.Span);
            }
        }
    }

    private static bool IsWithinInvalidProject(Uri sourceUri, IReadOnlyList<string> invalidProjectDirectories)
    {
        foreach (string directory in invalidProjectDirectories)
        {
            string relative = Path.GetRelativePath(directory, sourceUri.LocalPath);
            if (!Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExtension(SourceSnapshot snapshot, string extension)
        => Path.GetExtension(snapshot.Uri.LocalPath).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private sealed class SourceState(SourceAnalysisResult result, DiagnosticReporter reporter)
    {
        internal SourceAnalysisResult Result { get; set; } = result;
        internal DiagnosticReporter Reporter { get; } = reporter;
    }

    private static bool IsWithinDirectory(Uri uri, string directory)
    {
        string relative = Path.GetRelativePath(directory, uri.LocalPath);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private sealed record ProjectContext(string Directory, IReadOnlyList<SourceSnapshot> Members);
}

/// <summary>
/// Keeps one document's analysis result paired with diagnostics from that exact snapshot.
/// </summary>
/// <param name="Analysis">
/// The syntax and semantic analysis for the document.
/// </param>
/// <param name="Reporter">
/// Diagnostics whose spans belong to the analysis snapshot.
/// </param>
internal sealed record ProjectDiagnosticResult(AnalysisResult Analysis, IDiagnosticReporter Reporter);
