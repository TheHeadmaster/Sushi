using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies compiler-backed function completion through the ordinary
/// source-document LSP handler.
/// </summary>
[TestFixture]
public sealed class SourceCompletionHandlerTests
{
    [Test]
    public async Task FunctionPrefixShouldReturnMatchingVisibleFunctionsInDeterministicOrder()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 CalculateHealth(int32 health) { return health; }
            public int32 CalculateDamage(int32 damage, int32 multiplier) { return damage * multiplier; }
            public int32 Start() { return Calc; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return Calc"), CancellationToken.None);
            CompletionItem[] items = [.. completion.Items];

            items.Select(item => item.Label)
                .Should()
                .Equal("CalculateDamage", "CalculateHealth");

            items
                .Should()
                .OnlyContain(item => item.Kind == CompletionItemKind.Function);

            items[0].Detail
                .Should()
                .Be("int32 CalculateDamage(int32 damage, int32 multiplier)");

            completion.IsIncomplete
                .Should()
                .BeFalse();
        });
    }

    [Test]
    public async Task CompletionShouldReplaceTheEntireIdentifierWhenCursorIsInsideIt()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 CalculateDamage(int32 value) { return value; }
            public int32 Start() { return Calcuate; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return Calc"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("CalculateDamage");

            TextEdit? edit = item.TextEdit?.TextEdit;

            edit
                .Should()
                .NotBeNull();

            edit!.NewText
                .Should()
                .Be("CalculateDamage");

            int start = source.LastIndexOf("Calcuate", StringComparison.Ordinal);
            int end = start + "Calcuate".Length;

            (int startLine, int startCharacter) = GetPosition(source, start);
            (int endLine, int endCharacter) = GetPosition(source, end);

            edit.Range.Start.Line
                .Should()
                .Be(startLine);

            edit.Range.Start.Character
                .Should()
                .Be(startCharacter);

            edit.Range.End.Line
                .Should()
                .Be(endLine);

            edit.Range.End.Character
                .Should()
                .Be(endCharacter);
        });
    }

    [Test]
    public async Task FunctionCompletionShouldUseCaseSensitivePrefixes()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public int32 calculateOther(int32 value) { return value; }
            public int32 Start() { return calc; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return calc"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("calculateOther");
        });
    }

    [Test]
    public async Task CompletionShouldNotSuggestFunctionsInsideComments()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public int32 Start() { /* Calc */ return 1; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "/* Calc"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task CompletionShouldNotSuggestPackageFunctionsAfterMemberAccess()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public int32 Start() { return value.Calc; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "value.Calc"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task CompletionShouldNotSuggestFunctionsWhileDeclaringALocal()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public int32 Start() { int32 Calc = 1; return Calc; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "int32 Calc"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task EscapedFunctionNamesShouldRetainTheirRequiredSourceSpelling()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 @return(int32 value) { return value; }
            public int32 Start() { return ret; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return ret"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("@return");

            item.TextEdit!.TextEdit!.NewText
                .Should()
                .Be("@return");
        });
    }

    [Test]
    public async Task Utf16EditorPositionsShouldRemainCorrectAfterMultibyteSourceText()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public int32 Start() { /* 🍣 */ return Calc; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return Calc"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("Calculate");

            int offset = source.LastIndexOf("Calc", StringComparison.Ordinal);
            (int line, int character) = GetPosition(source, offset);

            item.TextEdit!.TextEdit!.Range.Start.Line
                .Should()
                .Be(line);

            item.TextEdit.TextEdit.Range.Start.Character
                .Should()
                .Be(character);
        });
    }

    [Test]
    public async Task UpdatedEditorSnapshotShouldReplaceEarlierCompletionCandidates()
    {
        const string initial = """
            package Example;
            namespace Example;
            public int32 CalculateOld(int32 value) { return value; }
            public int32 Start() { return Calc; }
            """;

        const string updated = """
            package Example;
            namespace Example;
            public int32 CalculateNew(int32 value) { return value; }
            public int32 Start() { return Calc; }
            """;

        string directory = CreateTemporaryDirectory();

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, initial);

            Uri uri = new(path);
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, initial, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            CompletionList before = await handler.Handle(At(initial, uri, "return Calc"), CancellationToken.None);

            before.Items.Select(item => item.Label)
                .Should()
                .Equal("CalculateOld");

            await workspace.ChangeDocument(uri, 2, updated, CancellationToken.None);

            CompletionList after = await handler.Handle(At(updated, uri, "return Calc"), CancellationToken.None);

            after.Items.Select(item => item.Label)
                .Should()
                .Equal("CalculateNew");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CrossFileCompletionShouldRespectCompilerVisibility()
    {
        string directory = CreateTemporaryDirectory();
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);

        string projectPath = Path.Combine(directory, "Completion.susproj");
        string declarationsPath = Path.Combine(sourcesDirectory, "Functions.sus");
        string callerPath = Path.Combine(sourcesDirectory, "Program.sus");

        const string project = """
            name = "Completion"
            assembly = "Completion"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;

        const string declarations = """
            package Example;
            namespace Example;
            public int32 CalculateDamage(int32 damage) { return damage; }
            private int32 CalculateSecret(int32 value) { return value; }
            """;

        const string caller = """
            package Example;
            namespace Example;
            public int32 Start() { return Calc; }
            """;

        try
        {
            await File.WriteAllTextAsync(projectPath, project);
            await File.WriteAllTextAsync(declarationsPath, declarations);
            await File.WriteAllTextAsync(callerPath, caller);

            Uri callerUri = new(callerPath);
            Uri folderUri = new(directory + Path.DirectorySeparatorChar);

            using WorkspaceOrchestrator workspace = new();
            await workspace.Initialize([new WorkspaceFolder
            {
                Uri = DocumentUri.From(folderUri),
                Name = "Completion"
            }], CancellationToken.None);

            await workspace.OpenDocument(callerUri, 1, caller, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            CompletionList completion = await handler.Handle(At(caller, callerUri, "return Calc"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("CalculateDamage");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UnsupportedDocumentTypesShouldReturnNoCompletionItems()
    {
        using WorkspaceOrchestrator workspace = new();
        SourceCompletionHandler handler = new(workspace);

        CompletionList completion = await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(new Uri("file:///Example.susproj"))),
            Position = new Position(0, 0)
        }, CancellationToken.None);

        completion.Items
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task CancellationShouldBeHonored()
    {
        using WorkspaceOrchestrator workspace = new();
        SourceCompletionHandler handler = new(workspace);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Func<Task> act = async () => await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(new Uri("file:///Example.sus"))),
            Position = new Position(0, 0)
        }, cancellation.Token);

        await act
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    private static CompletionParams At(string source, Uri uri, string precedingText)
    {
        int start = source.LastIndexOf(precedingText, StringComparison.Ordinal);

        start
            .Should()
            .BeGreaterThanOrEqualTo(0);

        (int line, int character) = GetPosition(source, start + precedingText.Length);

        return new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
            Position = new Position(line, character)
        };
    }

    private static (int Line, int Character) GetPosition(string source, int offset)
    {
        string prefix = source[..offset];
        int line = prefix.Count(character => character == '\n');
        int character = prefix.Length - prefix.LastIndexOf('\n') - 1;
        return (line, character);
    }

    private static async Task WithSource(string source, Func<SourceCompletionHandler, Uri, Task> test)
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, source);

            Uri uri = new(path);
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            await test(handler, uri);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Completion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}