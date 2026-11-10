using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies compiler-visible named argument labels in package-function calls.
/// </summary>
[TestFixture]
public sealed class SourceCompletionArgumentLabelHandlerTests
{
    private const string CursorMarker = "│";

    [Test]
    public async Task EmptyInvocationShouldOfferEveryLabelAndRetainPositionalValues()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start(int32 width) { Configure(│); return; }
            """);

        items.Should().Contain(item => item.Label == "width:");
        items.Should().Contain(item => item.Label == "visible:");
        items.Should().Contain(item => item.Label == "width");
        items.Where(item => item.Label is "width:" or "visible:")
            .Should()
            .OnlyContain(item => item.Kind == CompletionItemKind.Property);
        GetTextEdit(items.Single(item => item.Label == "width:")).NewText.Should().Be("width: ");
        items.Single(item => item.Label == "width").Kind.Should().Be(CompletionItemKind.Variable);
    }

    [TestCase("Configure(100, │);", "visible:")]
    [TestCase("Configure(100, 200, │);", "visible:")]
    [TestCase("Configure(visible: true, │);", "height:", "width:")]
    [TestCase("Configure(height: 2, width: 1, │);", "visible:")]
    [TestCase("Configure(1, visible: true, │);", "height:")]
    [TestCase("Configure(1, 2, visible: true, │);")]
    public async Task InvocationShouldOfferOnlyUnsuppliedParameters(string invocation, params string[] expected)
    {
        string source = $$"""
            package Example;
            namespace Example;
            public void Configure(int32 width, int32 height, bool visible) { return; }
            public void Start() { {{invocation}} return; }
            """;

        CompletionItem[] items = await Complete(source);
        items.Where(item => item.Kind == CompletionItemKind.Property)
            .Select(item => item.Label)
            .Should()
            .Equal(expected);
    }

    [Test]
    public async Task LabelPrefixShouldFilterCaseSensitivelyAndReplaceTheWholeIdentifier()
    {
        CompletionItem[] matching = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start() { Configure(vi│); return; }
            """);

        CompletionItem label = matching.Single(item => item.Label == "visible:");
        label.FilterText.Should().Be("visible");
        label.Detail.Should().Be("named argument label (bool)");
        GetTextEdit(label).NewText.Should().Be("visible: ");
        GetTextEdit(label).Range.Start.Character.Should().Be(32);
        GetTextEdit(label).Range.End.Character.Should().Be(34);

        CompletionItem[] wrongCase = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start() { Configure(VI│); return; }
            """);

        wrongCase.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();
    }

    [Test]
    public async Task ExistingColonShouldNotBeDuplicatedWhenReplacingPartialLabel()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start() { Configure(vis│ible: true); return; }
            """);

        CompletionItem label = items.Single(item => item.Label == "visible:");
        GetTextEdit(label).NewText.Should().Be("visible: ");
        GetTextEdit(label).Range.End.Character.Should().BeGreaterThan(GetTextEdit(label).Range.Start.Character);
    }

    [Test]
    public async Task EscapedParameterNamesShouldInsertTheirValidSourceSpelling()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Configure(bool @return) { return; }
            public void Start() { Configure(│); return; }
            """);

        CompletionItem label = items.Single(item => item.Label == "@return:");
        label.FilterText.Should().Be("@return");
        GetTextEdit(label).NewText.Should().Be("@return: ");
    }

    [Test]
    public async Task LabelAndValueWithTheSameNameShouldRemainDistinctItems()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Configure(bool enabled) { return; }
            public void Start(bool enabled) { Configure(│); return; }
            """);

        items.Should().Contain(item => item.Label == "enabled:"
            && item.Kind == CompletionItemKind.Property
            && GetTextEdit(item).NewText == "enabled: ");
        items.Should().Contain(item => item.Label == "enabled"
            && item.Kind == CompletionItemKind.Variable
            && GetTextEdit(item).NewText == "enabled");
    }

    [Test]
    public async Task NamedArgumentSlotsSuppressPositionalValuesAndValueCompletionResumesAfterColon()
    {
        CompletionItem[] nextSlot = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, int32 height, bool visible) { return; }
            public void Start(int32 amount) { Configure(visible: true, │); return; }
            """);

        nextSlot.Where(item => item.Kind == CompletionItemKind.Property)
            .Select(item => item.Label)
            .Should()
            .Equal("height:", "width:");
        nextSlot.Where(item => item.Kind != CompletionItemKind.Property).Should().BeEmpty();

        CompletionItem[] value = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start(int32 amount) { Configure(width: │); return; }
            """);

        value.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();
        value.Should().Contain(item => item.Label == "amount" && item.Kind == CompletionItemKind.Variable);
    }

    [Test]
    public async Task NestedInvocationShouldOfferOnlyTheInnermostCallLabels()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Inner(bool ready) { return; }
            public void Outer(int32 value) { return; }
            public void Start() { Outer(Inner(│)); return; }
            """);

        items.Where(item => item.Kind == CompletionItemKind.Property)
            .Select(item => item.Label)
            .Should()
            .Equal("ready:");
    }

    [Test]
    public async Task GroupingParenthesesShouldNotBeTreatedAsAnArgumentList()
    {
        CompletionItem[] items = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width) { return; }
            public void Start(int32 amount) { Configure((│)); return; }
            """);

        items.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();
    }

    [TestCase("UnknownFunction(│);")]
    [TestCase("object.Configure(│);")]
    [TestCase("Configure(width: 1, width: │);")]
    [TestCase("Configure(width: 1, 2, │);")]
    public async Task UnknownUnsupportedAndInvalidArgumentMappingsShouldNotOfferLabels(string invocation)
    {
        CompletionItem[] items = await Complete($$"""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start(int32 object) { {{invocation}} return; }
            """);

        items.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();
    }

    [Test]
    public async Task IncompleteAndMultilineInvocationsShouldOfferLabels()
    {
        CompletionItem[] incomplete = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start() { Configure(│
            """);

        incomplete.Where(item => item.Kind == CompletionItemKind.Property)
            .Select(item => item.Label)
            .Should()
            .Equal("visible:", "width:");

        CompletionItem[] multiline = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width, bool visible) { return; }
            public void Start() {
                Configure(
                    1,
                    │
                );
                return;
            }
            """);

        multiline.Where(item => item.Kind == CompletionItemKind.Property)
            .Select(item => item.Label)
            .Should()
            .Equal("visible:");
    }

    [Test]
    public async Task CommentsAndCompletedStatementsShouldNotOfferLabels()
    {
        CompletionItem[] comment = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width) { return; }
            public void Start() { // │
                return;
            }
            """);

        comment.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();

        CompletionItem[] statement = await Complete("""
            package Example;
            namespace Example;
            public void Configure(int32 width) { return; }
            public void Start() { int32 value = 1; │ return; }
            """);

        statement.Where(item => item.Kind == CompletionItemKind.Property).Should().BeEmpty();
    }

    private static TextEdit GetTextEdit(CompletionItem item)
    {
        item.TextEdit.Should().NotBeNull();
        item.TextEdit!.IsTextEdit.Should().BeTrue();
        return item.TextEdit.TextEdit!;
    }

    private static async Task<CompletionItem[]> Complete(string sourceWithMarker)
    {
        int offset = sourceWithMarker.IndexOf(CursorMarker, StringComparison.Ordinal);
        offset.Should().BeGreaterThanOrEqualTo(0);
        string source = sourceWithMarker.Remove(offset, CursorMarker.Length);
        (int line, int character) = GetPosition(source, offset);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing",
            "ArgumentLabels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, source);
            Uri uri = new(path);
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            CompletionList result = await handler.Handle(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
                Position = new Position(line, character)
            }, CancellationToken.None);

            return [.. result.Items];
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (int Line, int Character) GetPosition(string source, int offset)
    {
        string prefix = source[..offset];
        int line = prefix.Count(character => character == '\n');
        int character = prefix.Length - prefix.LastIndexOf('\n') - 1;
        return (line, character);
    }
}
