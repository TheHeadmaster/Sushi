using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Renderers.Normalize.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Serializes source-controlled documentation as safe Markdown for LSP Hover responses.
/// </summary>
internal static class SourceHoverMarkdown
{
    private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().Build();
    private static readonly char[] markdownPunctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".ToCharArray();

    /// <summary>
    /// Normalizes Markdown syntax while making raw HTML and unsafe resources inert.
    /// </summary>
    /// <param name="markdown">Documentation Markdown captured from a source snapshot.</param>
    /// <returns>Safe Markdown suitable for an LSP response that does not allow raw HTML.</returns>
    internal static string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        MarkdownDocument document = Markdown.Parse(markdown, pipeline);
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        NormalizeRenderer renderer = new(writer, new NormalizeOptions { ExpandAutoLinks = true });
        renderer.ObjectRenderers.Replace<NormalizeHtmlInlineRenderer>(new SafeHtmlInlineRenderer());
        renderer.ObjectRenderers.Replace<HtmlBlockRenderer>(new SafeHtmlBlockRenderer());
        renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new SafeLinkInlineRenderer());
        renderer.ObjectRenderers.Replace<AutolinkInlineRenderer>(new SafeAutolinkInlineRenderer());
        renderer.ObjectRenderers.Replace<LinkReferenceDefinitionGroupRenderer>(new OmitLinkReferenceDefinitionsRenderer());
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
        if (normalized.Length == 0 || normalized.Any(char.IsControl) || normalized.Any(char.IsWhiteSpace)
            || normalized.IndexOfAny(['<', '>', '\\', '"', '\'', '`']) >= 0 || normalized.Contains('%', StringComparison.Ordinal))
        {
            return false;
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? absoluteUri))
        {
            if (!absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            safeDestination = absoluteUri.AbsoluteUri;
            return true;
        }

        if (normalized.StartsWith("//", StringComparison.Ordinal) || HasUriScheme(normalized))
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
                return string.Empty;
            }

            if (StringComparer.Ordinal.Equals(next, decoded))
            {
                return decoded;
            }

            decoded = next;
        }

        return decoded.Contains('%', StringComparison.Ordinal) ? string.Empty : decoded;
    }

    private static bool HasUriScheme(string destination)
    {
        int colon = destination.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || !char.IsAsciiLetter(destination[0]))
        {
            return false;
        }

        for (int index = 1; index < colon; index++)
        {
            char character = destination[index];
            if (!char.IsAsciiLetterOrDigit(character) && character is not '+' and not '-' and not '.')
            {
                return false;
            }
        }

        return true;
    }

    private static string EscapeMarkdownLiteral(string value)
    {
        System.Text.StringBuilder escaped = new(value.Length * 2);
        foreach (char character in value)
        {
            if (Array.IndexOf(markdownPunctuation, character) >= 0)
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    private sealed class SafeHtmlInlineRenderer : NormalizeObjectRenderer<HtmlInline>
    {
        protected override void Write(NormalizeRenderer renderer, HtmlInline inline)
            => renderer.Write(EscapeMarkdownLiteral(inline.Tag));
    }

    private sealed class SafeHtmlBlockRenderer : NormalizeObjectRenderer<HtmlBlock>
    {
        protected override void Write(NormalizeRenderer renderer, HtmlBlock block)
            => renderer.Write(EscapeMarkdownLiteral(block.Lines.ToString()));
    }

    private sealed class SafeLinkInlineRenderer : NormalizeObjectRenderer<LinkInline>
    {
        protected override void Write(NormalizeRenderer renderer, LinkInline link)
        {
            string? destination = link.GetDynamicUrl?.Invoke() ?? link.Url;
            if (link.IsImage || !TryGetSafeDestination(destination, out string safeDestination))
            {
                renderer.WriteChildren(link);
                return;
            }

            renderer.Write('[');
            renderer.WriteChildren(link);
            renderer.Write("](<");
            renderer.Write(safeDestination);
            renderer.Write(">)");
        }
    }

    private sealed class SafeAutolinkInlineRenderer : NormalizeObjectRenderer<AutolinkInline>
    {
        protected override void Write(NormalizeRenderer renderer, AutolinkInline link)
        {
            if (!TryGetSafeDestination(link.Url, out string safeDestination))
            {
                renderer.Write(EscapeMarkdownLiteral(link.Url));
                return;
            }

            renderer.Write('[');
            renderer.Write(EscapeMarkdownLiteral(link.Url));
            renderer.Write("](<");
            renderer.Write(safeDestination);
            renderer.Write(">)");
        }
    }

    private sealed class OmitLinkReferenceDefinitionsRenderer : NormalizeObjectRenderer<LinkReferenceDefinitionGroup>
    {
        protected override void Write(NormalizeRenderer renderer, LinkReferenceDefinitionGroup group)
        {
        }
    }
}
