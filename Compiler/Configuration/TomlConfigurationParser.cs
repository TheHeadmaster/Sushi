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
            ConvertSpan(snapshot, diagnostic.Span));
    }

    private static SushiSourceSpan ConvertSpan(SourceSnapshot snapshot, TomlSourceSpan span)
    {
        int startUtf16 = NormalizeTomlynOffset(span.Start.Offset, snapshot.Text.Length);

        int endUtf16 = span.End.Offset < 0 ? startUtf16 : Math.Min(span.End.Offset + 1, snapshot.Text.Length);
        
        if (endUtf16 < startUtf16)
        {
            endUtf16 = startUtf16;
        }

        int startByte = Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, startUtf16));

        int endByte = Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, endUtf16));

        return new SushiSourceSpan(snapshot, startByte, endByte);
    }

    private static int NormalizeTomlynOffset(int offset, int textLength)
    {
        if (offset < 0)
        {
            return textLength;
        }

        return Math.Min(offset, textLength);
    }
}