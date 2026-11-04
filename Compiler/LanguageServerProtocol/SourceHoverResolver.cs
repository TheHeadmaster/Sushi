using System.Text;
using Sushi.Analysis;
using Sushi.Documentation;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves Hover only from compiler-bound occurrences owned by one source snapshot.
/// </summary>
internal static class SourceHoverResolver
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
        SourceSemanticOccurrence? occurrence = SourceSemanticOccurrenceLookup.FindOccurrence(
            analysis.SemanticOccurrences, analysis.Snapshot, offset);
        if (occurrence is null)
        {
            return null;
        }

        string? markdown = Format(occurrence.Symbol, cancellationToken);
        return markdown is null ? null : new SourceHoverResult(occurrence.Span, markdown);
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
            string prose = SanitizeMarkdown(RenderDocumentationText(documentation.Content.Prose, documentation.Content, cancellationToken));
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
                        parameterDescriptions.Add($"- `{name}`: {SanitizeMarkdown(RenderDocumentationText(directive.Description, documentation.Content, cancellationToken))}");
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
                    sections.Add("**Returns**\n\n" + SanitizeMarkdown(RenderDocumentationText(returnDirective.Description, documentation.Content, cancellationToken)));
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
                sections.Add(SanitizeMarkdown(RenderDocumentationText(parameter.Description, documentation.Content, cancellationToken)));
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


    private static string RenderDocumentationText(DocumentationText text, DocumentationContent content, CancellationToken cancellationToken)
    {
        StringBuilder rendered = new();
        ReadOnlySpan<byte> source = text.Snapshot.Bytes.Span;
        for (int index = 0; index < text.Lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index > 0)
            {
                rendered.Append('\\n');
            }

            SourceSpan line = text.Lines[index];
            int position = line.Start;
            foreach (DocumentationReference reference in content.InlineReferences.Where(item =>
                item.Span.Start >= line.Start && item.Span.End <= line.End))
            {
                cancellationToken.ThrowIfCancellationRequested();
                rendered.Append(Encoding.UTF8.GetString(source[position..reference.Span.Start]));
                string replacement = reference.Kind is DocumentationReferenceKind.Link
                    ? reference.DisplayText ?? reference.WrittenTarget ?? string.Empty
                    : reference.TargetNameSpelling ?? string.Empty;
                rendered.Append(replacement);
                position = reference.Span.End;
            }

            rendered.Append(Encoding.UTF8.GetString(source[position..line.End]));
        }

        return rendered.ToString();
    }

    internal static string SanitizeMarkdown(string value) => SourceHoverMarkdown.Render(value);
}

/// <summary>
/// Contains Markdown and its exact source-token range for a source Hover response.
/// </summary>
/// <param name="Range">The semantic occurrence's source range.</param>
/// <param name="Markdown">The safe Markdown presentation.</param>
internal sealed record SourceHoverResult(SourceSpan Range, string Markdown);