using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

[TestFixture]
public sealed class ParameterInlayHintHandlerTests
{
    [Test]
    public async Task ModesShouldApplyDirectIdentifierSuppressionAndRespectNamedArgumentsAndUtf16Positions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "ParameterInlayHints " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Hints.sus");
        Uri uri = new(path);
        const string source = """
            package Example;
            namespace Example;
            public void Display(int32 count) {}
            public void Pair(int32 left, int32 right) {}
            /// 😀 preceding multibyte text
            public void Start(int32 count, int32 value) { Display(count); Display(value); Display(42); Display(count + 1); Display((count)); Pair(right: 2, left: 1); }
            """;

        try
        {
            await File.WriteAllTextAsync(path, source);
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);
            ParameterInlayHintHandler handler = new(workspace);
            string[] lines = source.Split('\n');
            InlayHintParams request = new()
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(0, 0), new Position(lines.Length - 1, lines[^1].Length))
            };

            workspace.ParameterInlayHintMode.Should().Be(ParameterInlayHintMode.Selective);
            InlayHintContainer? selective = await handler.Handle(request, CancellationToken.None);
            selective!.Select(hint => hint.Label.String).Should().Equal("count:", "count:", "count:", "count:");
            selective.All(hint => hint.Kind is InlayHintKind.Parameter && hint.PaddingRight is true).Should().BeTrue();

            int literalOffset = source.IndexOf("Display(42)", StringComparison.Ordinal) + "Display(".Length;
            int literalLine = source[..literalOffset].Count(character => character is '\n');
            int literalCharacter = source[(source[..literalOffset].LastIndexOf('\n') + 1)..literalOffset].Length;
            InlayHintParams literalRangeRequest = new()
            {
                TextDocument = request.TextDocument,
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(literalLine, literalCharacter), new Position(literalLine, literalCharacter + 2))
            };
            (await handler.Handle(literalRangeRequest, CancellationToken.None))!
                .Select(hint => hint.Label.String).Should().Equal("count:");

            await workspace.SetParameterInlayHintModeAsync(ParameterInlayHintMode.All, CancellationToken.None);
            InlayHintContainer? all = await handler.Handle(request, CancellationToken.None);
            all!.Select(hint => hint.Label.String).Should().Equal("count:", "count:", "count:", "count:", "count:");

            ParameterInlayHintConfigurationHandler configurationHandler = new(workspace);
            await configurationHandler.Handle(new DidChangeConfigurationParams
            {
                Settings = JObject.Parse("{ 'sushi': { 'parameterInlayHints': { 'mode': 'off' } } }")
            }, CancellationToken.None);
            (await handler.Handle(request, CancellationToken.None))!.Should().BeEmpty();

            int callOffset = source.IndexOf("Display(value)", StringComparison.Ordinal) + "Display(".Length;
            int utf16Line = source[..callOffset].Count(character => character is '\n');
            int utf16Character = source[(source[..callOffset].LastIndexOf('\n') + 1)..callOffset].Length;
            selective!.First(hint => hint.Label.String == "count:" && hint.Position.Line == utf16Line).Position.Character
                .Should().Be(utf16Character);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UnsupportedDocumentTypesShouldReturnNoHints()
    {
        using WorkspaceOrchestrator workspace = new();
        ParameterInlayHintHandler handler = new(workspace);
        InlayHintContainer? result = await handler.Handle(new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(new Uri("file:///Example.susproj"))),
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(new Position(0, 0), new Position(0, 10))
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
