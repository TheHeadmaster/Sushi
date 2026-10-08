using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

/// <summary>
/// Coordinates compiler analysis for a specific type of source snapshot.
/// </summary>
public abstract class Analyzer
{
    /// <summary>
    /// Accumulates encoding diagnostics into the specified diagnostic reporter.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter to accumulate the diagnostics into.
    /// </param>
    /// <param name="snapshot">
    /// The source snapshot containing the diagnostic encoding issues.
    /// </param>
    protected static void AccumulateEncodingDiagnostics(IDiagnosticReporter diagnosticReporter, SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (SourceEncodingIssue issue in snapshot.EncodingIssues)
        {
            diagnosticReporter.GenerateError(ErrorType.InvalidUtf8, new SourceSpan(snapshot, issue.Start, issue.End));
        }
    }
}