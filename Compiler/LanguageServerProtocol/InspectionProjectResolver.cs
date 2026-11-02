using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves project membership using workspace project identities and the compiler's configured source discovery.
/// </summary>
internal static class InspectionProjectResolver
{
    internal sealed record Match(string ProjectPath, IReadOnlyList<string> Sources);

    /// <summary>
    /// Finds all matching projects deterministically, retaining configuration failures rather than guessing membership.
    /// </summary>
    internal static async Task<IReadOnlyList<Match>> Resolve(InspectionWorkspaceSnapshot inputs, string? explicitProjectUri, CancellationToken cancellationToken)
    {
        Uri? explicitProject = null;
        if (explicitProjectUri is not null && (!Uri.TryCreate(explicitProjectUri, UriKind.Absolute, out explicitProject) || !explicitProject.IsFile))
        {
            throw new InspectionInputException("The explicit project must identify a local .susproj file.");
        }

        if (explicitProject is not null && !inputs.Projects.Any(uri => uri.IsSamePath(explicitProject)))
        {
            throw new InspectionInputException("The selected project is not known to the current workspace.");
        }

        List<Match> matches = [];
        foreach (Uri projectUri in inputs.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (explicitProject is not null && !projectUri.IsSamePath(explicitProject))
            {
                continue;
            }

            string projectDirectory = Path.GetDirectoryName(projectUri.LocalPath)!;
            string relative = Path.GetRelativePath(projectDirectory, inputs.Selected.Uri.LocalPath);
            // A source configuration may include parent-relative paths, so membership itself is decided by the matcher.
            SourceSnapshot configuration = await ProjectSourceLoader.LoadSnapshot(projectUri.LocalPath, cancellationToken);
            IDiagnosticReporter diagnostics = new DiagnosticReporter();
            ProjectAnalysisResult result = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(configuration, diagnostics, cancellationToken);
            if (diagnostics.HasErrors() || result.Project is null)
            {
                // An invalid containing project cannot be silently skipped in favor of another project.
                if (explicitProject is not null || (!Path.IsPathRooted(relative) && relative != ".."
                    && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                {
                    throw new InspectionInputException("Fix the containing Sushi project configuration before requesting inspection.");
                }

                continue;
            }

            IReadOnlyList<string> paths = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, result.Project, cancellationToken);
            if (paths.Any(path => new Uri(path).IsSamePath(inputs.Selected.Uri)))
            {
                matches.Add(new Match(projectUri.LocalPath, paths));
            }
        }

        return matches;
    }
}