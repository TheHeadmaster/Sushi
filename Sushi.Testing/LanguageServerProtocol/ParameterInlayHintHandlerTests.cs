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
            selective!.Select(hint => GetLabelText(hint)).Should().Equal("count:", "count:", "count:", "count:");
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
                .Select(hint => GetLabelText(hint)).Should().Equal("count:");

            await workspace.SetParameterInlayHintModeAsync(ParameterInlayHintMode.All, CancellationToken.None);
            InlayHintContainer? all = await handler.Handle(request, CancellationToken.None);
            all!.Select(hint => GetLabelText(hint)).Should().Equal("count:", "count:", "count:", "count:", "count:");

            ParameterInlayHintConfigurationHandler configurationHandler = new(workspace);
            await configurationHandler.Handle(new DidChangeConfigurationParams
            {
                Settings = JObject.Parse("{ 'sushi': { 'parameterInlayHints': { 'mode': 'off' } } }")
            }, CancellationToken.None);
            (await handler.Handle(request, CancellationToken.None))!.Should().BeEmpty();

            int callOffset = source.IndexOf("Display(value)", StringComparison.Ordinal) + "Display(".Length;
            int utf16Line = source[..callOffset].Count(character => character is '\n');
            int utf16Character = source[(source[..callOffset].LastIndexOf('\n') + 1)..callOffset].Length;
            selective!.First(hint => GetLabelText(hint) == "count:" && hint.Position.Line == utf16Line).Position.Character
                .Should().Be(utf16Character);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CrossFileHintsShouldIncludeCurrentParameterDocumentationAndDeclarationLocation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "InteractiveParameterHints " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        string projectPath = Path.Combine(directory, "InteractiveHints.susproj");
        string mathPath = Path.Combine(sourcesDirectory, "Math.sus");
        string programPath = Path.Combine(sourcesDirectory, "Program.sus");
        Uri mathUri = new(mathPath);
        Uri programUri = new(programPath);
        const string project = """
            name = "InteractiveHints"
            assembly = "InteractiveHints"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;
        const string initialMath = """
            package Example;
            namespace Example;
            /// Doubles an integer.
            /// @param value The integer to double.
            /// @return The doubled integer.
            public int32 Double(int32 value) { return value * 2; }
            /// Duplicate parameter descriptions should not be selected.
            /// @param item First item description.
            /// @param item Second item description.
            /// @return The item value.
            public int32 DuplicateDocs(int32 item) { return item; }
            public int32 Unannotated(int32 sample) { return sample; }
            """;
        const string updatedMath = """
            package Example;
            namespace Example;
            /// Doubles an integer.
            /// @param amount The integer to double after editing.
            /// @return The doubled integer.
            public int32 Double(int32 amount) { return amount * 2; }
            /// Duplicate parameter descriptions should not be selected.
            /// @param item First item description.
            /// @param item Second item description.
            /// @return The item value.
            public int32 DuplicateDocs(int32 item) { return item; }
            public int32 Unannotated(int32 sample) { return sample; }
            """;
        const string programSource = """
            package Example;
            namespace Example;
            public int32 Calculate() { int32 duplicate = DuplicateDocs(2); int32 plain = Unannotated(3); return Double(duplicate + plain); }
            """;

        try
        {
            await File.WriteAllTextAsync(projectPath, project);
            await File.WriteAllTextAsync(mathPath, initialMath);
            await File.WriteAllTextAsync(programPath, programSource);
            using WorkspaceOrchestrator workspace = new();
            Uri folderUri = new(directory + Path.DirectorySeparatorChar);
            await workspace.Initialize([new WorkspaceFolder
            {
                Uri = DocumentUri.From(folderUri),
                Name = "InteractiveHints"
            }], CancellationToken.None);
            await workspace.OpenDocument(programUri, 1, programSource, CancellationToken.None);
            ParameterInlayHintHandler handler = new(workspace);
            string[] lines = programSource.Split('\n');
            InlayHintParams request = new()
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(programUri)),
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(0, 0), new Position(lines.Length - 1, lines[^1].Length))
            };

            InlayHintContainer? initialResult = await handler.Handle(request, CancellationToken.None);
            InlayHint[] initialHints = initialResult is null ? [] : [.. initialResult];
            initialHints.Should().HaveCount(3);
            InlayHintLabelPart duplicateLabel = GetLabelPart(initialHints, "item:");
            duplicateLabel.Tooltip!.MarkupContent!.Value.Should().Contain("parameter int32 item in DuplicateDocs")
                .And.NotContain("First item description")
                .And.NotContain("Second item description");
            AssertDefinitionLocation(duplicateLabel.Location!, mathUri, initialMath, "item");

            InlayHintLabelPart unannotatedLabel = GetLabelPart(initialHints, "sample:");
            unannotatedLabel.Tooltip!.MarkupContent!.Value.Should().Contain("parameter int32 sample in Unannotated")
                .And.NotContain("description");
            AssertDefinitionLocation(unannotatedLabel.Location!, mathUri, initialMath, "sample");

            InlayHintLabelPart documentedLabel = GetLabelPart(initialHints, "value:");
            documentedLabel.Tooltip!.MarkupContent!.Value.Should().Contain("parameter int32 value in Double")
                .And.Contain("The integer to double.");
            AssertDefinitionLocation(documentedLabel.Location!, mathUri, initialMath, "value");

            await workspace.OpenDocument(mathUri, 2, updatedMath, CancellationToken.None);
            InlayHintContainer? updatedResult = await handler.Handle(request, CancellationToken.None);
            InlayHint[] updatedHints = updatedResult is null ? [] : [.. updatedResult];
            InlayHintLabelPart updatedLabel = GetLabelPart(updatedHints, "amount:");
            updatedLabel.Tooltip!.MarkupContent!.Value.Should().Contain("parameter int32 amount in Double")
                .And.Contain("The integer to double after editing.");
            AssertDefinitionLocation(updatedLabel.Location!, mathUri, updatedMath, "amount");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static InlayHintLabelPart GetLabelPart(IEnumerable<InlayHint> hints, string value)
        => hints.SelectMany(hint => hint.Label.InlayHintLabelParts?.ToArray() ?? Array.Empty<InlayHintLabelPart>())
            .Single(part => StringComparer.Ordinal.Equals(part.Value, value));

    private static void AssertDefinitionLocation(Location location, Uri expectedUri, string source, string parameterName)
    {
        location.Uri.ToUri().LocalPath.Should().Be(Path.GetFullPath(expectedUri.LocalPath));
        int nameStart = source.IndexOf("int32 " + parameterName, StringComparison.Ordinal) + "int32 ".Length;
        string[] precedingLines = source[..nameStart].Split('\n');
        int expectedLine = precedingLines.Length - 1;
        int expectedCharacter = precedingLines[^1].Length;
        location.Range.Start.Line.Should().Be(expectedLine);
        location.Range.Start.Character.Should().Be(expectedCharacter);
        location.Range.End.Line.Should().Be(expectedLine);
        location.Range.End.Character.Should().Be(expectedCharacter + parameterName.Length);
    }

    private static string GetLabelText(InlayHint hint)
        => hint.Label.HasString
            ? hint.Label.String ?? string.Empty
            : string.Concat(hint.Label.InlayHintLabelParts!.Select(part => part.Value));

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
