using System.Text;
using Sushi.Diagnostics;
using Sushi.Source;
using Tomlyn.Parsing;
using Tomlyn.Syntax;
using SushiSourceSpan = Sushi.Source.SourceSpan;
using TomlSourceSpan = Tomlyn.Syntax.SourceSpan;

namespace Sushi.Configuration;

public sealed class TomlConfigurationParser
{
    // Placeholder for now, will change the code to pick between SUSE and SUSWARN later
    private const string TomlSyntaxErrorDiagnosticCode = "SUSE1000";

    public TomlConfigurationParseResult Parse(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentSyntax syntax = SyntaxParser.Parse(snapshot.Text, sourceName: snapshot.Uri.LocalPath, validate: true);

        cancellationToken.ThrowIfCancellationRequested();

        SushiDiagnostic[] diagnostics = [.. syntax.Diagnostics.Select(diagnostic => ConvertDiagnostic(snapshot, diagnostic))];

        return new TomlConfigurationParseResult(snapshot, syntax, diagnostics);
    }

    private static SushiDiagnostic ConvertDiagnostic(SourceSnapshot snapshot, DiagnosticMessage diagnostic)
    {
        return new SushiDiagnostic(
            TomlSyntaxErrorDiagnosticCode,
            diagnostic.Message,
            diagnostic.Kind switch
            {
                DiagnosticMessageKind.Error => DiagnosticSeverity.Error,
                DiagnosticMessageKind.Warning => DiagnosticSeverity.Warning,
                _ => throw new ArgumentOutOfRangeException(nameof(diagnostic))
            },
            diagnostic.Span.ToSushiSpan(snapshot));
    }
}