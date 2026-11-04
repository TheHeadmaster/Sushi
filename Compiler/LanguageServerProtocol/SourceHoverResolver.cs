using System.Text.RegularExpressions;
using Sushi.Analysis;
using Sushi.Documentation;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves Hover only from compiler-bound occurrences owned by one source snapshot.
/// </summary>
internal static partial class SourceHoverResolver
{
    /// <summary>
    /// Finds the semantic occurrence at a canonical UTF-8 source offset and formats its metadata.
    /// </summary>
    /// <param name="analysis">The current analyzed source snapshot.</param>
    /// <param name="offset">The byte offset at the editor cursor.</param>
    /// <param name="cancellationToken">Cancels occurrence selection and formatting.</param>
    /// <returns>A formatted Hover result, or null for unsupported or unresolved source.</returns>
    internal static SourceHoverResult? Resolve(SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();
        SourceSemanticOccurrence? occurrence = FindOccurrence(analysis.SemanticOccurrences, analysis.Snapshot, offset);
        if (occurrence is null)
        {
            return null;
        }

        string? markdown = Format(occurrence.Symbol, cancellationToken);
        return markdown is null ? null : new SourceHoverResult(occurrence.Span, markdown);
    }


    private static SourceSemanticOccurrence? FindOccurrence(
        IReadOnlyList<SourceSemanticOccurrence> occurrences,
        SourceSnapshot snapshot,
        int offset)
    {
        int low = 0;
        int high = occurrences.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (occurrences[middle].Span.Start <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int candidate = low - 1;
        if (candidate < 0)
        {
            return null;
        }

        SourceSemanticOccurrence occurrence = occurrences[candidate];
        return ReferenceEquals(occurrence.Span.Snapshot, snapshot) && offset < occurrence.Span.End ? occurrence : null;
    }

    private static string? Format(SourceSemanticSymbol symbol, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> sections = [];
        string? signature = FormatSignature(symbol);
        if (signature is null)
        {
            return null;
        }

        sections.Add($"```sushi\n{signature}\n```");
        DocumentationBlock? documentation = symbol.Documentation;
        if (documentation is not null && symbol.Kind is SourceSemanticSymbolKind.Function or SourceSemanticSymbolKind.Activity or SourceSemanticSymbolKind.ActivityMethod)
        {
            string prose = SanitizeMarkdown(documentation.Content.Prose.Text);
            if (!string.IsNullOrWhiteSpace(prose))
            {
                sections.Add(prose);
            }

            if (symbol.Callable is BoundFunction callable)
            {
                List<string> parameterDescriptions = [];
                IGrouping<string?, DocumentationDirective>[] parameterGroups = [.. documentation.Content.Directives
                    .Where(item => item.Kind is DocumentationDirectiveKind.Parameter)
                    .GroupBy(item => item.ParameterName, StringComparer.Ordinal)];
                foreach (IGrouping<string?, DocumentationDirective> parameterGroup in parameterGroups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (parameterGroup.Skip(1).Any())
                    {
                        continue;
                    }

                    DocumentationDirective directive = parameterGroup.First();
                    if (directive.ParameterName is string name && !string.IsNullOrWhiteSpace(directive.Description.Text)
                        && callable.Parameters.Any(parameter => StringComparer.Ordinal.Equals(parameter.Name, name)))
                    {
                        parameterDescriptions.Add($"- `{name}`: {SanitizeMarkdown(directive.Description.Text)}");
                    }
                }

                if (parameterDescriptions.Count > 0)
                {
                    sections.Add("**Parameters**\n\n" + string.Join("\n", parameterDescriptions));
                }

                DocumentationDirective[] returnDirectives = [.. documentation.Content.Directives
                    .Where(item => item.Kind is DocumentationDirectiveKind.Return)];
                DocumentationDirective? returnDirective = returnDirectives.Length == 1 ? returnDirectives[0] : null;
                if (callable.ReturnType is not BoundType.Void && returnDirective is not null
                    && !string.IsNullOrWhiteSpace(returnDirective.Description.Text))
                {
                    sections.Add("**Returns**\n\n" + SanitizeMarkdown(returnDirective.Description.Text));
                }
            }
        }
        else if (documentation is not null && symbol.Kind is SourceSemanticSymbolKind.Parameter)
        {
            DocumentationDirective[] parameterDirectives = [.. documentation.Content.Directives.Where(item =>
                item.Kind is DocumentationDirectiveKind.Parameter && StringComparer.Ordinal.Equals(item.ParameterName, symbol.Name))];
            DocumentationDirective? parameter = parameterDirectives.Length == 1 ? parameterDirectives[0] : null;
            if (parameter is not null && !string.IsNullOrWhiteSpace(parameter.Description.Text))
            {
                sections.Add(SanitizeMarkdown(parameter.Description.Text));
            }
        }

        return string.Join("\n\n", sections);
    }

    private static string? FormatSignature(SourceSemanticSymbol symbol)
    {
        if (symbol.Kind is SourceSemanticSymbolKind.Activity)
        {
            return $"{GetAccessibility(symbol.Accessibility ?? Accessibility.Private)} activity {symbol.Name}";
        }

        if (symbol.Kind is SourceSemanticSymbolKind.Type)
        {
            return $"type {GetTypeName(symbol.Type)}";
        }

        if (symbol.Kind is SourceSemanticSymbolKind.Parameter)
        {
            string containing = symbol.ContainingName is null ? "callable" : $"in {symbol.ContainingName}";
            return $"parameter {GetTypeName(symbol.Type)} {symbol.Name} {containing}";
        }

        if (symbol.Kind is SourceSemanticSymbolKind.Local)
        {
            return $"local {GetTypeName(symbol.Type)} {symbol.Name}";
        }

        if (symbol.Callable is not BoundFunction function)
        {
            return null;
        }

        string parameters = string.Join(", ", function.Parameters.Select(parameter => $"{GetTypeName(parameter.Type)} {parameter.Name}"));
        return symbol.Kind switch
        {
            SourceSemanticSymbolKind.Function => $"{GetAccessibility(function.Accessibility)} {GetTypeName(function.ReturnType)} {function.Name}({parameters})",
            SourceSemanticSymbolKind.ActivityMethod => $"{GetAccessibility(function.Accessibility)} {GetTypeName(function.ReturnType)} {function.Name}({parameters})",
            _ => null
        };
    }

    private static string SanitizeMarkdown(string value)
    {
        string[] lines = value.Split('\n');
        System.Text.StringBuilder safe = new(value.Length);
        char fenceMarker = '\0';
        int fenceLength = 0;
        for (int index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                safe.Append('\n');
            }

            string line = lines[index];
            if (fenceMarker != '\0')
            {
                if (IsClosingFence(line, fenceMarker, fenceLength))
                {
                    fenceMarker = '\0';
                    fenceLength = 0;
                }

                safe.Append(line);
                continue;
            }

            if (TryReadOpeningFence(line, out char marker, out int length))
            {
                fenceMarker = marker;
                fenceLength = length;
                safe.Append(line);
                continue;
            }

            string safeLine = RawHtmlTagRegex().Replace(line, match => $"&lt;{match.Groups[1].Value}&gt;");
            safe.Append(UnsafeLinkRegex().Replace(safeLine, "](#unsafe-link)"));
        }

        return safe.ToString();
    }

    private static bool TryReadOpeningFence(string line, out char marker, out int length)
    {
        int position = 0;
        while (position < line.Length && position < 3 && line[position] == ' ')
        {
            position++;
        }

        marker = position < line.Length ? line[position] : '\0';
        if (marker is not '`' and not '~')
        {
            length = 0;
            return false;
        }

        int start = position++;
        while (position < line.Length && line[position] == marker)
        {
            position++;
        }

        length = position - start;
        if (length < 3 || (marker == '`' && line[position..].Contains('`', StringComparison.Ordinal)))
        {
            marker = '\0';
            length = 0;
            return false;
        }

        return true;
    }

    private static bool IsClosingFence(string line, char marker, int minimumLength)
    {
        int position = 0;
        while (position < line.Length && position < 3 && line[position] == ' ')
        {
            position++;
        }

        int start = position;
        while (position < line.Length && line[position] == marker)
        {
            position++;
        }

        if (position - start < minimumLength)
        {
            return false;
        }

        return line[position..].All(char.IsWhiteSpace);
    }

    private static string GetAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Package => "package",
        Accessibility.Protected => "protected",
        Accessibility.Private => "private",
        _ => ""
    };

    private static string GetTypeName(BoundType? type) => type switch
    {
        BoundType.Int32 => "int32",
        BoundType.Bool => "bool",
        BoundType.Void => "void",
        _ => "unknown"
    };

    [GeneratedRegex("<(/?[A-Za-z][^>]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex RawHtmlTagRegex();

    [GeneratedRegex(@"\]\(\s*(?:javascript|data|vbscript):[^)]*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeLinkRegex();
}

/// <summary>
/// Contains Markdown and its exact source-token range for a source Hover response.
/// </summary>
/// <param name="Range">The semantic occurrence's source range.</param>
/// <param name="Markdown">The safe Markdown presentation.</param>
internal sealed record SourceHoverResult(SourceSpan Range, string Markdown);
