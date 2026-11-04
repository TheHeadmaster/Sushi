using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Renders source-controlled documentation Markdown into inert, presentation-only HTML.
/// </summary>
internal static class SourceHoverMarkdown
{
    private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>
    /// Parses Markdown with raw HTML disabled and renders only safe link destinations.
    /// </summary>
    /// <param name="markdown">Documentation Markdown captured from a source snapshot.</param>
    /// <returns>HTML generated from the Markdown syntax tree, safe to include in LSP Markdown content.</returns>
    internal static string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        MarkdownDocument document = Markdown.Parse(markdown, pipeline);
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HtmlRenderer renderer = new(writer);
        renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new SafeLinkInlineRenderer());
        renderer.ObjectRenderers.Replace<AutolinkInlineRenderer>(new SafeAutolinkInlineRenderer());
        renderer.Render(document);
        return writer.ToString();
    }

    private static bool TryGetSafeDestination(string? destination, out string safeDestination)
    {
        safeDestination = string.Empty;
        if (string.IsNullOrWhiteSpace(destination))
        {
            return false;
        }

        string normalized = DecodeDestination(destination);
        string schemeCandidate = string.Concat(normalized
            .Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character) && character != '\\'));
        if (Uri.TryCreate(schemeCandidate, UriKind.Absolute, out Uri? absoluteUri))
        {
            if (absoluteUri.Scheme is not ("http" or "https"))
            {
                return false;
            }

            safeDestination = absoluteUri.AbsoluteUri;
            return true;
        }

        if (schemeCandidate.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        int colon = schemeCandidate.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && IsUriScheme(schemeCandidate.AsSpan(0, colon)))
        {
            return false;
        }

        safeDestination = normalized;
        return true;
    }

    private static string DecodeDestination(string destination)
    {
        string decoded = destination;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            string next;
            try
            {
                next = Uri.UnescapeDataString(System.Net.WebUtility.HtmlDecode(decoded));
            }
            catch (UriFormatException)
            {
                return decoded;
            }

            if (StringComparer.Ordinal.Equals(next, decoded))
            {
                return decoded;
            }

            decoded = next;
        }

        return decoded.Contains('%', StringComparison.Ordinal) ? string.Empty : decoded;
    }

    private static bool IsUriScheme(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not '+' and not '-' and not '.')
            {
                return false;
            }
        }

        return true;
    }

    private sealed class SafeLinkInlineRenderer : LinkInlineRenderer
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            string? destination = link.GetDynamicUrl?.Invoke() ?? link.Url;
            if (link.IsImage || !renderer.EnableHtmlForInline || !TryGetSafeDestination(destination, out string safeDestination))
            {
                renderer.WriteChildren(link);
                return;
            }

            renderer.Write("<a href=\"");
            renderer.WriteEscapeUrl(safeDestination);
            renderer.Write('"');
            if (!string.IsNullOrEmpty(link.Title))
            {
                renderer.Write(" title=\"");
                renderer.WriteEscape(link.Title);
                renderer.Write('"');
            }

            renderer.Write('>');
            renderer.WriteChildren(link);
            renderer.Write("</a>");
        }
    }

    private sealed class SafeAutolinkInlineRenderer : AutolinkInlineRenderer
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline link)
        {
            if (link.IsEmail || !renderer.EnableHtmlForInline || !TryGetSafeDestination(link.Url, out string safeDestination))
            {
                renderer.WriteEscape(link.Url);
                return;
            }

            renderer.Write("<a href=\"");
            renderer.WriteEscapeUrl(safeDestination);
            renderer.Write("\">");
            renderer.WriteEscape(link.Url);
            renderer.Write("</a>");
        }
    }
}
