using Microsoft.Extensions.FileSystemGlobbing;
using Sushi.Analysis;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Compilation;

/// <summary>
/// Coordinates the temporary executable MVP from project configuration and Sushi source through native linking.
/// </summary>
/// <remarks>
/// The MVP requires exactly one successfully bound function across the project's selected source files.
/// Selecting that function as the process root is an implementation convention and does not define Sushi
/// source-level entry-point semantics.
/// </remarks>
public sealed class MVPProjectCompiler(MVPExecutableBuilder executableBuilder)
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    /// <summary>
    /// Creates a project compiler using the temporary default executable pipeline.
    /// </summary>
    internal MVPProjectCompiler() : this(new MVPExecutableBuilder())
    {
    }

    /// <summary>
    /// Compiles a Sushi project into a native executable using the current MVP backend pipeline.
    /// </summary>
    /// <param name="projectOrFolderPath">
    /// A <c>.susproj</c> path or a directory containing exactly one Sushi project file.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel compilation.
    /// </param>
    /// <returns>
    /// The project compilation result.
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
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
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
            SourceAnalysisResult source = (SourceAnalysisResult)await this.sourceAnalyzer.Analyze(
                snapshot, diagnosticReporter, cancellationToken);

            sources.Add(source);
        }

        if (diagnosticReporter.HasErrors())
        {
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
        }

        BoundActivity? root = RootActivityAnalyzer.SelectRoot(
            sources, projectSnapshot, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors() || root is null)
        {
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
        }

        if (root.Lifetime is not ActivityLifetime.Task)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedRootActivityLifetime, root.Declaration.LifetimeKeyword.Span);
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
        }

        LoweredFunction? entry = BindAndLowerRootLifecycleMethod(
            root.Entry, "__sushi_root_entry", diagnosticReporter, cancellationToken);

        LoweredFunction? stop = root.Stop is null
            ? null
            : BindAndLowerRootLifecycleMethod(root.Stop, "__sushi_root_stop", diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors() || entry is null || (root.Stop is not null && stop is null))
        {
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
        }

        string outputPath = GetMVPOutputPath(projectDirectory, project);

        NativeExecutableArtifact executable = await executableBuilder.BuildRootActivityAsync(
            entry, stop, outputPath, cancellationToken);

        return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable);
    }

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

    private static BoundFunction SelectMVPEntryFunction(IReadOnlyList<BoundFunction> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);

        return functions.Count switch
        {
            1 => functions[0],
            0 => throw new NotSupportedException("The executable MVP requires exactly one successfully bound Sushi function, but the project contains none."),
            _ => throw new NotSupportedException($"The executable MVP requires exactly one successfully bound Sushi function, but the project contains {functions.Count}.")
        };
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
    /// Binds one supported root lifecycle body and lowers it under a private compiler-generated symbol.
    /// </summary>
    /// <param name="method">
    /// The validated activity lifecycle declaration.
    /// </param>
    /// <param name="symbol">
    /// The compiler-owned native symbol.</param>
    /// <param name="diagnosticReporter">Receives semantic and MVP-subset diagnostics.</param>
    /// <param name="cancellationToken">Cancels binding and lowering.</param>
    /// <returns>The lowered function, or null if the method cannot be compiled.</returns>
    private static LoweredFunction? BindAndLowerRootLifecycleMethod(BoundActivityMethod method, string symbol, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (method.Declaration.Parameters.Count != 0)
        {
            diagnosticReporter.GenerateError(
                ErrorType.UnsupportedRootLifecycleParameters,
                method.Declaration.OpenParenthesisToken.Span);

            return null;
        }

        if (method.Declaration.Body.Statements.Count > 1)
        {
            diagnosticReporter.GenerateError(
                ErrorType.UnsupportedRootLifecycleBody,
                method.Declaration.Body.Span);

            return null;
        }

        BoundBlock? body = FunctionDeclarationBinder.BindBody(
            method.Declaration.Body,
            BoundType.Void,
            [],
            diagnosticReporter,
            cancellationToken);

        if (body is null)
        {
            return null;
        }

        BoundFunction function = new(method.Accessibility, symbol, BoundType.Void, body);
        IRFunction structured = BoundFunctionIRConverter.Convert(function, cancellationToken);

        return IRFunctionLowerer.Lower(structured, cancellationToken);
    }
}