using System.Text;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Parses documentation directives and explicit references from source-backed lines while respecting Markdown code boundaries.
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
        List<DocumentationReference> references = [];
        Fence? fence = null;
        int inlineCodeDelimiterLength = 0;
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
                inlineCodeDelimiterLength = 0;
                fence = newFence;
                continue;
            }

            bool[] inlineCode = ParseInlineReferences(lines, index, source, lines[index], content, references, ref inlineCodeDelimiterLength, cancellationToken);
            DirectiveCandidate? candidate = ParseDirective(lines[index], content, inlineCode, diagnosticReporter);
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

        return new DocumentationContent(new DocumentationText(snapshot, proseLines), directives, references);
    }

    private static bool[] ParseInlineReferences(IReadOnlyList<Line> lines, int lineIndex, ReadOnlySpan<byte> source,
        Line line, ReadOnlySpan<byte> content, ICollection<DocumentationReference> references,
        ref int inlineCodeDelimiterLength, CancellationToken cancellationToken)
    {
        bool[] inlineCode = MarkInlineCode(content, lines, index, source, ref inlineCodeDelimiterLength, cancellationToken);
        for (int index = 0; index < content.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inlineCode[index] || content[index] != (byte)'{' || IsEscaped(content, index))
            {
                continue;
            }

            DocumentationReferenceKind? kind = StartsWith(content, index, "{@link ")
                ? DocumentationReferenceKind.Link
                : StartsWith(content, index, "{@param ") ? DocumentationReferenceKind.Parameter : null;
            if (kind is null)
            {
                continue;
            }

            int close = FindReferenceClose(content, index + 1, cancellationToken);
            if (close < 0)
            {
                continue;
            }

            SourceSpan construct = new(line.FullSpan.Snapshot, line.Content.Start + index, line.Content.Start + close + 1);
            int bodyOffset = kind is DocumentationReferenceKind.Link ? 7 : 8;
            ReadOnlySpan<byte> body = content[(index + bodyOffset)..close];
            int absoluteBodyStart = line.Content.Start + index + bodyOffset;
            references.Add(kind is DocumentationReferenceKind.Link
                ? ParseLinkReference(construct, body, absoluteBodyStart)
                : ParseParameterReference(construct, body, absoluteBodyStart));
            index = close;
        }

        return inlineCode;
    }

    private static int FindReferenceClose(ReadOnlySpan<byte> content, int start, CancellationToken cancellationToken)
    {
        int nestedBraces = 0;
        for (int position = start; position < content.Length; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEscaped(content, position))
            {
                continue;
            }

            if (content[position] == (byte)'{')
            {
                nestedBraces++;
            }
            else if (content[position] == (byte)'}')
            {
                if (nestedBraces == 0)
                {
                    return position;
                }

                nestedBraces--;
            }
        }

        return -1;
    }

    private static DocumentationReference ParseLinkReference(SourceSpan construct, ReadOnlySpan<byte> body, int bodyStart)
    {
        int separator = body.IndexOf((byte)'|');
        ReadOnlySpan<byte> targetSource = separator < 0 ? body : body[..separator];
        ReadOnlySpan<byte> displaySource = separator < 0 ? ReadOnlySpan<byte>.Empty : body[(separator + 1)..];
        ReadOnlySpan<byte> target = TrimHorizontalWhitespace(targetSource, out int targetLeading);
        ReadOnlySpan<byte> display = TrimHorizontalWhitespace(displaySource, out int displayLeading);
        int targetStart = bodyStart + targetLeading;
        SourceSpan targetSpan = new(construct.Snapshot, targetStart, targetStart + target.Length);
        SourceSpan? displaySpan = separator < 0
            ? null
            : new SourceSpan(construct.Snapshot, bodyStart + separator + 1 + displayLeading,
                bodyStart + separator + 1 + displayLeading + display.Length);
        int openParenthesis = target.IndexOf((byte)'(');
        ReadOnlySpan<byte> qualifiedName = openParenthesis < 0 ? target : target[..openParenthesis];
        List<SourceSpan> parameterTypeSpans = [];
        bool wellFormed = !qualifiedName.IsEmpty;

        if (openParenthesis >= 0)
        {
            if (target[^1] != (byte)')')
            {
                wellFormed = false;
            }
            else
            {
                ReadOnlySpan<byte> typeList = target[(openParenthesis + 1)..^1];
                int typeStart = 0;
                while (typeStart < typeList.Length)
                {
                    int comma = typeList[typeStart..].IndexOf((byte)',');
                    int typeEnd = comma < 0 ? typeList.Length : typeStart + comma;
                    ReadOnlySpan<byte> sourceType = typeList[typeStart..typeEnd];
                    ReadOnlySpan<byte> type = TrimHorizontalWhitespace(sourceType, out int typeLeading);
                    if (type.IsEmpty)
                    {
                        wellFormed = false;
                    }
                    else
                    {
                        int typeAbsoluteStart = targetStart + openParenthesis + 1 + typeStart + typeLeading;
                        parameterTypeSpans.Add(new SourceSpan(construct.Snapshot, typeAbsoluteStart, typeAbsoluteStart + type.Length));
                    }

                    if (comma < 0)
                    {
                        break;
                    }

                    typeStart = typeEnd + 1;
                    if (typeStart == typeList.Length)
                    {
                        wellFormed = false;
                    }
                }
            }
        }

        int finalSeparator = qualifiedName.LastIndexOf((byte)'.');
        ReadOnlySpan<byte> name = finalSeparator < 0 ? qualifiedName : qualifiedName[(finalSeparator + 1)..];
        SourceSpan? qualifierSpan = finalSeparator < 0
            ? null
            : new SourceSpan(construct.Snapshot, targetStart, targetStart + finalSeparator);
        SourceSpan? nameSpan = null;
        if (!name.IsEmpty)
        {
            int componentOffset = finalSeparator < 0 ? 0 : finalSeparator + 1;
            int nameStart = targetStart + componentOffset;
            nameSpan = new SourceSpan(construct.Snapshot, nameStart, nameStart + name.Length);
        }
        else
        {
            wellFormed = false;
        }

        if (qualifiedName.IsEmpty || qualifiedName[0] == (byte)'.' || qualifiedName[^1] == (byte)'.')
        {
            wellFormed = false;
        }

        return new DocumentationReference(DocumentationReferenceKind.Link, construct, nameSpan, targetSpan,
            qualifierSpan, parameterTypeSpans, displaySpan, openParenthesis >= 0, wellFormed);
    }

    private static DocumentationReference ParseParameterReference(SourceSpan construct, ReadOnlySpan<byte> body, int bodyStart)
    {
        ReadOnlySpan<byte> target = TrimHorizontalWhitespace(body, out int leadingWhitespace);
        bool wellFormed = !target.IsEmpty;
        foreach (byte value in target)
        {
            if (IsHorizontalWhitespace(value))
            {
                wellFormed = false;
            }
        }

        SourceSpan? targetSpan = target.IsEmpty
            ? null
            : new SourceSpan(construct.Snapshot, bodyStart + leadingWhitespace, bodyStart + leadingWhitespace + target.Length);
        return new DocumentationReference(DocumentationReferenceKind.Parameter, construct, targetSpan, targetSpan,
            null, [], null, false, wellFormed);
    }

    private static bool[] MarkInlineCode(ReadOnlySpan<byte> content, IReadOnlyList<Line> lines, int lineIndex, ReadOnlySpan<byte> source, ref int openDelimiterLength, CancellationToken cancellationToken)
    {
        bool[] marked = new bool[content.Length];
        int position = 0;
        if (openDelimiterLength > 0)
        {
            int closing = FindMatchingBacktickRun(content, 0, openDelimiterLength, cancellationToken);
            if (closing < 0)
            {
                Array.Fill(marked, true);
                return marked;
            }

            int closingEnd = FindBacktickRunEnd(content, closing, cancellationToken);
            Array.Fill(marked, true, 0, closingEnd);
            openDelimiterLength = 0;
            position = closingEnd;
        }

        while (position < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (content[position] != 96 || IsEscaped(content, position))
            {
                position++;
                continue;
            }

            int runEnd = FindBacktickRunEnd(content, position, cancellationToken);
            int runLength = runEnd - position;
            int closing = FindMatchingBacktickRun(content, runEnd, runLength, cancellationToken);
            if (closing < 0)
            {
                if (HasMatchingBacktickRun(lines, lineIndex + 1, source, runLength, cancellationToken))
                {
                    Array.Fill(marked, true, position, content.Length - position);
                    openDelimiterLength = runLength;
                    break;
                }

                position = runEnd;
                continue;
            }

            int closingEnd = FindBacktickRunEnd(content, closing, cancellationToken);
            Array.Fill(marked, true, position, closingEnd - position);
            position = closingEnd;
        }

        return marked;
    }

    private static bool HasMatchingBacktickRun(
        IReadOnlyList<Line> lines,
        int startLine,
        ReadOnlySpan<byte> source,
        int expectedLength,
        CancellationToken cancellationToken)
    {
        Fence? fence = null;
        for (int lineIndex = startLine; lineIndex < lines.Count; lineIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> content = GetBytes(source, lines[lineIndex].Content);
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

            if (FindMatchingBacktickRun(content, 0, expectedLength, cancellationToken) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingBacktickRun(ReadOnlySpan<byte> content, int start, int expectedLength, CancellationToken cancellationToken)
    {
        for (int position = start; position < content.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (content[position] != 96)
            {
                position++;
                continue;
            }

            int runEnd = FindBacktickRunEnd(content, position, cancellationToken);
            if (runEnd - position == expectedLength && !IsEscaped(content, position))
            {
                return position;
            }

            position = runEnd;
        }

        return -1;
    }

    private static int FindBacktickRunEnd(ReadOnlySpan<byte> content, int start, CancellationToken cancellationToken)
    {
        int end = start + 1;
        while (end < content.Length && content[end] == 96)
        {
            cancellationToken.ThrowIfCancellationRequested();
            end++;
        }

        return end;
    }

    private static bool IsEscaped(ReadOnlySpan<byte> content, int position)
    {
        int slashes = 0;
        for (int index = position - 1; index >= 0 && content[index] == (byte)'\\'; index--)
        {
            slashes++;
        }

        return (slashes & 1) != 0;
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, int start, string value)
        => content[start..].StartsWith(Encoding.ASCII.GetBytes(value));

    private static ReadOnlySpan<byte> TrimHorizontalWhitespace(ReadOnlySpan<byte> value, out int leadingWhitespace)
    {
        leadingWhitespace = 0;
        while (leadingWhitespace < value.Length && IsHorizontalWhitespace(value[leadingWhitespace]))
        {
            leadingWhitespace++;
        }

        int end = value.Length;
        while (end > leadingWhitespace && IsHorizontalWhitespace(value[end - 1]))
        {
            end--;
        }

        return value[leadingWhitespace..end];
    }

    private static DirectiveCandidate? ParseDirective(Line line, ReadOnlySpan<byte> content, bool[] inlineCode, IDiagnosticReporter diagnosticReporter)
    {
        int position = SkipHorizontalWhitespace(content, 0);
        if (position >= content.Length || content[position] != (byte)'@' || inlineCode[position])
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