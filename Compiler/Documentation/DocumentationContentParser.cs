using System.Text;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Parses directive structure from source-backed documentation lines without interpreting Markdown.
/// </summary>
internal static class DocumentationContentParser
{
    public static DocumentationContent Parse(SourceSnapshot snapshot, IReadOnlyList<SourceSpan> lineSpans, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(lineSpans);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        ReadOnlySpan<byte> source = snapshot.Bytes.Span;
        List<Line> lines = [];
        foreach (SourceSpan lineSpan in lineSpans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int contentStart = Math.Min(lineSpan.Start + 3, lineSpan.End);
            if (contentStart < lineSpan.End && source[contentStart] == (byte)' ')
            {
                contentStart++;
            }

            lines.Add(new Line(lineSpan, new SourceSpan(snapshot, contentStart, lineSpan.End)));
        }

        DirectiveCandidate?[] candidates = new DirectiveCandidate?[lines.Count];
        Fence? fence = null;
        for (int index = 0; index < lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> content = GetBytes(source, lines[index].Content);

            if (fence is Fence openFence)
            {
                if (IsClosingFence(content, openFence))
                {
                    fence = null;
                }

                continue;
            }

            if (TryReadFence(content, out Fence newFence))
            {
                fence = newFence;
                continue;
            }

            DirectiveCandidate? candidate = ParseDirective(lines[index], content, diagnosticReporter);
            candidates[index] = candidate;
        }

        List<DocumentationDirective> directives = [];
        List<SourceSpan> proseLines = [];
        for (int index = 0; index < lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectiveCandidate? candidate = candidates[index];
            if (candidate is not DirectiveCandidate recognized)
            {
                proseLines.Add(lines[index].Content);
                continue;
            }

            int lastLine = index;
            List<SourceSpan> descriptionLines = [recognized.DescriptionSpan];
            while (lastLine + 1 < lines.Count && candidates[lastLine + 1] is null)
            {
                lastLine++;
                descriptionLines.Add(lines[lastLine].Content);
            }

            SourceSpan completeSpan = new(snapshot, lines[index].FullSpan.Start, lines[lastLine].FullSpan.End);
            DocumentationText description = new(snapshot, descriptionLines);
            directives.Add(new DocumentationDirective(
                recognized.Kind,
                recognized.NameSpan,
                recognized.ParameterName,
                recognized.ParameterNameSpan,
                completeSpan,
                description));
            if (string.IsNullOrWhiteSpace(description.Text))
            {
                diagnosticReporter.GenerateWarning(WarningType.EmptyDocumentationDescription, recognized.NameSpan);
            }

            index = lastLine;
        }

        return new DocumentationContent(new DocumentationText(snapshot, proseLines), directives);
    }

    private static DirectiveCandidate? ParseDirective(Line line, ReadOnlySpan<byte> content, IDiagnosticReporter diagnosticReporter)
    {
        int position = SkipHorizontalWhitespace(content, 0);
        if (position >= content.Length || content[position] != (byte)'@')
        {
            return null;
        }

        if (position > 0 && content[position - 1] == (byte)'\\')
        {
            return null;
        }

        int nameStart = position;
        position++;
        while (position < content.Length && !IsHorizontalWhitespace(content[position]))
        {
            position++;
        }

        if (position == nameStart + 1)
        {
            return null;
        }

        string name = Encoding.UTF8.GetString(content[(nameStart + 1)..position]);
        SourceSpan nameSpan = new(line.FullSpan.Snapshot, line.Content.Start + nameStart, line.Content.Start + position);
        int afterName = SkipHorizontalWhitespace(content, position);

        if (StringComparer.Ordinal.Equals(name, "param"))
        {
            int parameterStart = afterName;
            while (afterName < content.Length && !IsHorizontalWhitespace(content[afterName]))
            {
                afterName++;
            }

            SourceSpan parameterSpan = new(line.FullSpan.Snapshot, line.Content.Start + parameterStart, line.Content.Start + afterName);
            string parameterName = Encoding.UTF8.GetString(content[parameterStart..afterName]);
            if (parameterName.StartsWith('@'))
            {
                parameterName = parameterName[1..];
            }

            int descriptionStart = SkipHorizontalWhitespace(content, afterName);
            SourceSpan descriptionSpan = new(line.FullSpan.Snapshot, line.Content.Start + descriptionStart, line.Content.End);
            return new DirectiveCandidate(DocumentationDirectiveKind.Parameter, nameSpan, parameterName, parameterSpan, descriptionSpan);
        }

        if (StringComparer.Ordinal.Equals(name, "return"))
        {
            SourceSpan descriptionSpan = new(line.FullSpan.Snapshot, line.Content.Start + afterName, line.Content.End);
            return new DirectiveCandidate(DocumentationDirectiveKind.Return, nameSpan, null, null, descriptionSpan);
        }

        diagnosticReporter.GenerateWarning(WarningType.UnrecognizedDocumentationDirective, nameSpan);
        return null;
    }

    private static bool TryReadFence(ReadOnlySpan<byte> content, out Fence fence)
    {
        int position = 0;
        while (position < content.Length && position < 3 && content[position] == (byte)' ')
        {
            position++;
        }

        if (position >= content.Length || (content[position] != (byte)'`' && content[position] != (byte)'~'))
        {
            fence = default;
            return false;
        }

        byte marker = content[position];
        int end = position;
        while (end < content.Length && content[end] == marker)
        {
            end++;
        }

        int length = end - position;
        if (length < 3 || (marker == (byte)'`' && content[end..].Contains((byte)'`')))
        {
            fence = default;
            return false;
        }

        fence = new Fence(marker, length);
        return true;
    }

    private static bool IsClosingFence(ReadOnlySpan<byte> content, Fence openFence)
    {
        int position = 0;
        while (position < content.Length && position < 3 && content[position] == (byte)' ')
        {
            position++;
        }

        int markerStart = position;
        while (position < content.Length && content[position] == openFence.Marker)
        {
            position++;
        }

        if (position - markerStart < openFence.Length)
        {
            return false;
        }

        return SkipHorizontalWhitespace(content, position) == content.Length;
    }

    private static int SkipHorizontalWhitespace(ReadOnlySpan<byte> content, int position)
    {
        while (position < content.Length && IsHorizontalWhitespace(content[position]))
        {
            position++;
        }

        return position;
    }

    private static bool IsHorizontalWhitespace(byte value)
        => value is (byte)' ' or (byte)'\t';

    private static ReadOnlySpan<byte> GetBytes(ReadOnlySpan<byte> source, SourceSpan span)
        => source[span.Start..span.End];

    private readonly record struct Line(SourceSpan FullSpan, SourceSpan Content);

    private readonly record struct Fence(byte Marker, int Length);

    private readonly record struct DirectiveCandidate(
        DocumentationDirectiveKind Kind,
        SourceSpan NameSpan,
        string? ParameterName,
        SourceSpan? ParameterNameSpan,
        SourceSpan DescriptionSpan);
}
