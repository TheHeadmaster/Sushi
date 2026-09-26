using Sushi.Configuration.Toml;
using Sushi.Diagnostics;
using Sushi.Source;
using Tomlyn;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

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

        TomlParser parser = TomlParser.Create(snapshot.Text,
            new TomlParserOptions
            {
                Mode = TomlParserMode.Tolerant,
                DecodeScalars = true
            },
            new TomlSerializerOptions
            {
                SourceName = snapshot.Uri.LocalPath
            });

        TomlConfigurationTable document = BuildDocument(snapshot, parser, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SushiDiagnostic[] diagnostics = [.. syntax.Diagnostics.Select(diagnostic => ConvertDiagnostic(snapshot, diagnostic))];

        return new TomlConfigurationParseResult(snapshot, syntax, document, diagnostics);
    }

    private static TomlConfigurationTable BuildDocument(SourceSnapshot snapshot, TomlParser parser, CancellationToken cancellationToken)
    {
        // Placeholder until this gets fleshed out
        return new(
            new Source.SourceSpan()
        );
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