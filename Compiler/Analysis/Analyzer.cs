using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Analysis;

public abstract class Analyzer
{
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