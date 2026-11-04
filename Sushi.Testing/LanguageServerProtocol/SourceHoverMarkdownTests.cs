using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies the safety boundary used to present source-controlled documentation Markdown.
/// </summary>
[TestFixture]
public sealed class SourceHoverMarkdownTests
{
    [Test]
    public void RawHtmlShouldRemainInertAcrossLinesAndInsideMarkdownCode()
    {
        const string markdown = """
            <script
            onclick="run()">alert(1)</script>
            <!-- hidden -->
            <svg onload="run()"></svg>

            `<img onerror="run()">`

            ```html
            <script>run()</script>
            [active](javascript:run())
            ```
            """;

        string rendered = SourceHoverResolver.SanitizeMarkdown(markdown);
        MatchCollection tags = Regex.Matches(rendered, "<([A-Za-z][A-Za-z0-9]*)\\b([^>]*)>", RegexOptions.CultureInvariant);

        rendered.Should()
            .NotContain("<script")
            .And.NotContain("<svg")
            .And.NotContain("<img")
            .And.NotContain("<!--")
            .And.Contain("&lt;script")
            .And.Contain("&lt;img");
        tags.Cast<Match>().Select(match => match.Groups[1].Value).Should().BeSubsetOf(["p", "pre", "code", "br"]);
        tags.Cast<Match>().Select(match => match.Groups[2].Value)
            .Where(attributes => Regex.IsMatch(attributes, "\\son[a-z]+\\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Should()
            .BeEmpty();
    }

    [Test]
    public void UnsafeLinksShouldBecomeInertTextAndSafeMarkdownShouldKeepItsPresentation()
    {
        const string markdown = """
            **Bold** and *emphasis* with `inline code`.

            - a list item
            - [web](https://example.com/path)
            - [relative](../guide.md)
            - [fragment](#section)
            - [mixed](JaVaScRiPt:alert(1))
            - [spaces](java%09script%3Aalert(1))
            - [entity](jav&#x61;script&#58;alert(1))
            - [escaped](javascript\:alert(1))
            - [reference][danger]
            - <javascript:alert(1)>
            - ![remote image](https://example.com/tracker.png)

            [danger]: vbscript:run()
            """;

        string rendered = SourceHoverResolver.SanitizeMarkdown(markdown);
        MatchCollection links = Regex.Matches(rendered, "<a\\s+href=\"([^\"]*)\"[^>]*>", RegexOptions.CultureInvariant);
        string[] destinations = links.Select(match => match.Groups[1].Value).ToArray();

        rendered.Should()
            .Contain("<strong>Bold</strong>")
            .And.Contain("<em>emphasis</em>")
            .And.Contain("<code>inline code</code>")
            .And.Contain("<ul>")
            .And.Contain("javascript:alert(1)")
            .And.Contain("remote image")
            .And.NotContain("<img");
        destinations.Should()
            .Contain("https://example.com/path")
            .And.Contain("../guide.md")
            .And.Contain("#section")
            .And.HaveCount(3);
        destinations.Should().Equal("https://example.com/path", "../guide.md", "#section");
    }

    [Test]
    public void UnicodeAndMultilineMarkdownDescriptionsShouldBePreserved()
    {
        const string markdown = """
            Cálcula el total 世界.

            Continued **description** with `value`.

            ```sushi
            int32 total = 1;
            ```
            """;

        string rendered = SourceHoverResolver.SanitizeMarkdown(markdown);

        rendered.Should()
            .Contain("Cálcula el total 世界.")
            .And.Contain("Continued <strong>description</strong>")
            .And.Contain("<code>value</code>")
            .And.Contain("int32 total = 1;");
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
}
