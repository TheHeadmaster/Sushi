
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Resolves the unique root activity declared by an executable project's own source files.
/// Referenced assembly declarations must not be included in the supplied source collection.
/// </summary>
public static class RootActivityAnalyzer
{
    /// <summary>
    /// Validates project-wide root uniqueness and resolves its semantic declaration.
    /// </summary>
    /// <param name="sources">
    /// Analysis results for source files belonging to the executable project's own assembly.
    /// </param>
    /// <param name="projectSnapshot">
    /// The project source used to locate a missing-root diagnostic.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives root selection diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels root discovery.
    /// </param>
    /// <returns>
    /// The unique successfully bound root, or null if no valid unique root can be selected.
    /// </returns>
    public static BoundActivity? SelectRoot(
        IReadOnlyList<SourceAnalysisResult> sources,
        SourceSnapshot projectSnapshot,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(projectSnapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        List<ActivityDeclarationSyntax> roots = [];

        foreach (SourceAnalysisResult source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (source.SyntaxTree.Root is not SourceFileSyntax sourceFile)
            {
                throw new ArgumentException("Root discovery requires complete source-file analysis results.", nameof(sources));
            }

            foreach (ActivityDeclarationSyntax declaration in sourceFile.ActivityDeclarations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (declaration.RootKeyword is { IsSourceBacked: true })
                {
                    roots.Add(declaration);
                }
            }
        }

        if (roots.Count == 0)
        {
            diagnosticReporter.GenerateError(ErrorType.MissingRootActivity, new SourceSpan(projectSnapshot, 0, 0));
            return null;
        }

        roots.Sort(static (left, right) =>
        {
            int pathOrder = string.CompareOrdinal(left.Span.Snapshot.Uri.AbsoluteUri, right.Span.Snapshot.Uri.AbsoluteUri);
            return pathOrder != 0 ? pathOrder : left.Span.Start.CompareTo(right.Span.Start);
        });

        if (roots.Count > 1)
        {
            for (int i = 1; i < roots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnosticReporter.GenerateError(ErrorType.DuplicateRootActivity, roots[i].RootKeyword!.Value.Span);
            }

            return null;
        }

        ActivityDeclarationSyntax root = roots[0];

        foreach (SourceAnalysisResult source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (BoundActivity activity in source.Activities)
            {
                if (ReferenceEquals(activity.Declaration, root))
                {
                    return activity;
                }
            }
        }

        return null;
    }
}
