using FluentAssertions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies that source documentation remains useful Markdown without exposing active HTML or resources.
/// </summary>
[TestFixture]
public sealed class SourceHoverMarkdownTests
{
    private static readonly MarkdownPipeline testPipeline = new MarkdownPipelineBuilder().Build();

    [Test]
    public void SafeMarkdownShouldRetainFormattingLinksListsAndFencedCode()
    {
        const string markdown = """
            **Bold** and *emphasis* with `inline code`.

            - a list item
            - [web](https://example.com/path)
            - [relative](../guide.md)
            - [fragment](#section)

            ```sushi
            int32 total = 1;
            ```
            """;

        string rendered = SourceHoverMarkdown.Render(markdown);
        MarkdownDocument parsed = ParseMarkdown(rendered);

        rendered.Should()
            .Contain("**Bold**")
            .And.Contain("*emphasis*")
            .And.Contain("`inline code`")
            .And.Contain("```sushi")
            .And.Contain("int32 total = 1;");
        parsed.Descendants().OfType<EmphasisInline>().Select(emphasis => emphasis.DelimiterCount)
            .Should()
            .Contain(1)
            .And.Contain(2);
        parsed.Descendants().OfType<CodeInline>().Should().ContainSingle();
        parsed.Descendants().OfType<ListBlock>().Should().ContainSingle();
        parsed.Descendants().OfType<FencedCodeBlock>().Should().ContainSingle();
        parsed.Descendants().OfType<LinkInline>().Where(link => !link.IsImage).Select(link => link.Url)
            .Should()
            .Equal("https://example.com/path", "../guide.md", "#section");
    }

    [Test]
    public void RawHtmlShouldBeInertAcrossLinesAndInsideInlineAndFencedCode()
    {
        const string markdown = """
            <script
            onclick="run()">[active](javascript:run())</script>
            <!-- hidden -->
            <svg onload="run()"></svg>
            &#x3C;iframe src=x&#x3E;
            Inline `<img onerror="run()">` text.

            ```html
            <script>run()</script>
            [active](javascript:run())
            ```
            """;

        string rendered = SourceHoverMarkdown.Render(markdown);
        MarkdownDocument parsed = ParseMarkdown(rendered);

        parsed.Descendants().OfType<HtmlBlock>().Should().BeEmpty();
        parsed.Descendants().OfType<HtmlInline>().Should().BeEmpty();
        parsed.Descendants().OfType<LinkInline>().Should().BeEmpty();
        parsed.Descendants().OfType<FencedCodeBlock>().Should().ContainSingle(block => block.Lines.ToString().Contains("<script>run()</script>", StringComparison.Ordinal));
        parsed.Descendants().OfType<CodeInline>().Should().ContainSingle(code => code.Content.Contains("<img onerror=\"run()\">", StringComparison.Ordinal));
        rendered.Should().Contain("\\<script");
    }

    [Test]
    public void UnsafeAndObfuscatedDestinationsShouldNotRemainActionable()
    {
        const string markdown = """
            - [mixed](JaVaScRiPt:alert(1))
            - [spaces](java%09script%3Aalert(1))
            - [entity](jav&#x61;script&#58;alert(1))
            - [escaped](javascript\:alert(1))
            - [data](data:text/html,script)
            - [vbscript](vbscript:run())
            - [reference][danger]
            - <javascript:alert(1)>
            - ![remote image](https://example.com/tracker.png)

            [danger]: vbscript:run()
            """;

        string rendered = SourceHoverMarkdown.Render(markdown);
        MarkdownDocument parsed = ParseMarkdown(rendered);

        parsed.Descendants().OfType<LinkInline>().Should().BeEmpty();
        parsed.Descendants().OfType<LinkReferenceDefinitionGroup>().Should().BeEmpty();
        rendered.Should()
            .Contain("javascript")
            .And.Contain("remote image")
            .And.NotContain("vbscript:")
            .And.NotContain("![remote image]");
    }

    [Test]
    public void UnicodeAndMultilineDescriptionsShouldRemainReadableMarkdown()
    {
        const string markdown = """
            Cálcula el total 世界.

            Continued **description** with `value`.

            @param value The first line.
            Continued with *formatted* text.

            @return The resulting 世界 value.
            """;

        string rendered = SourceHoverMarkdown.Render(markdown);
        MarkdownDocument parsed = ParseMarkdown(rendered);

        parsed.Descendants().OfType<EmphasisInline>().Should().Contain(emphasis => emphasis.DelimiterCount == 2);
        parsed.Descendants().OfType<CodeInline>().Should().ContainSingle();
        rendered.Should()
            .Contain("Cálcula el total 世界.")
            .And.Contain("Continued **description**")
            .And.Contain("@param value The first line.")
            .And.Contain("Continued with *formatted* text.")
            .And.Contain("@return The resulting 世界 value.");
    }

    [Test]
    public async Task SourceHoverShouldPropagateCancellationBeforeWorkspaceAnalysis()
    {
        using WorkspaceOrchestrator workspace = new();
        SourceHoverHandler handler = new(workspace);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        Func<Task> request = async () => await handler.Handle(new HoverParams(), cancellation.Token);

        await request.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task ConcurrentSourceHoverAnalysisShouldKeepOneCurrentSnapshot()
    {
        const string source = "package Example; namespace Example;\nprivate int32 Resolve(int32 value) { int32 result = value; return result; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Concurrent Hover " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "ConcurrentHover.sus");
        await File.WriteAllTextAsync(path, source);
        Uri uri = new(path);
        using WorkspaceOrchestrator workspace = new();
        try
        {
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceAnalysisResult?[] analyses = await Task.WhenAll(
                workspace.GetSourceHoverAnalysis(uri, CancellationToken.None),
                workspace.GetSourceHoverAnalysis(uri, CancellationToken.None));

            analyses.Where(analysis => analysis is null).Should().BeEmpty();
            analyses.Select(analysis => analysis!.Snapshot).Distinct(ReferenceEqualityComparer.Instance)
                .Should()
                .ContainSingle();
            analyses.SelectMany(analysis => analysis!.SemanticOccurrences)
                .Should()
                .Contain(occurrence => occurrence.Symbol.Name.Equals("result", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MarkdownDocument ParseMarkdown(string markdown) => Markdown.Parse(markdown, testPipeline);
}
