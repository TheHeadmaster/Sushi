using Sushi.Source;

namespace Sushi.Diagnostics;

public static class SourceEncodingDiagnostics
{
    // Placeholder until diagnostic numbering is settled.
    private const string InvalidUtf8Code =
        "SUSE0001";

    public static IReadOnlyList<SushiDiagnostic> Create(SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return
        [
            .. snapshot.EncodingIssues.Select(
                issue =>
                    new SushiDiagnostic(
                        InvalidUtf8Code,
                        "Malformed UTF-8 source sequence.",
                        DiagnosticSeverity.Error,
                        new SourceSpan(snapshot, issue.Start, issue.End)))
        ];
    }
}