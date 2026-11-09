using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies completion recovery while a statement-starting identifier
/// has not yet become a syntactically recognized Sushi statement.
/// </summary>
[TestFixture]
public sealed class SourceCompletionIncompleteStatementTests
{
    [TestCase("res")]
    [TestCase("res = 2;")]
    [TestCase("res += 1;")]
    [TestCase("res++;")]
    public async Task StatementStartingIdentifierShouldCompleteAnEarlierLocal(string statement)
    {
        string source = $$"""
            package Example;
            namespace Example;
            public void Start()
            {
                int32 result = 1;
                {{statement}}
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "res"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label.Should().Be("result");
            item.Kind.Should().Be(CompletionItemKind.Variable);
            item.Detail.Should().Be("local int32 result");
            item.TextEdit!.TextEdit!.NewText.Should().Be("result");
        });
    }

    [Test]
    public async Task ParameterShouldCompleteAtTheBeginningOfAnUnfinishedStatement()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start(int32 amount)
            {
                amo
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "amo"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label.Should().Be("amount");
            item.Detail.Should().Be("parameter int32 amount");
        });
    }

    [Test]
    public async Task OuterLocalShouldCompleteInsideAnUnfinishedNestedBlock()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start()
            {
                int32 result = 1;
                if (true)
                {
                    res
                }
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "res"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("result");
        });
    }

    [Test]
    public async Task LocalInClosedConditionalShouldNotAppearInAnUnfinishedOuterStatement()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start()
            {
                if (true)
                {
                    int32 inside = 1;
                }

                ins
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "ins"), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    [Test]
    public async Task LocalFromEarlierFunctionShouldNotAppearInUnfinishedNextFunction()
    {
        const string source = """
            package Example;
            namespace Example;
            public void First()
            {
                int32 result = 1;
            }

            public void Second()
            {
                res
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "res"), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    [Test]
    public async Task UnparsedIdentifierOutsideClosedFunctionShouldNotReceiveLocalCompletion()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start()
            {
                int32 result = 1;
            }

            res
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "res"), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    private static CompletionParams At(string source, Uri uri, string precedingText)
    {
        int index = source.LastIndexOf(precedingText, StringComparison.Ordinal);
        index.Should().BeGreaterThanOrEqualTo(0);

        string prefix = source[..(index + precedingText.Length)];
        int line = prefix.Count(character => character == '\n');
        int character = prefix.Length - prefix.LastIndexOf('\n') - 1;

        return new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
            Position = new Position(line, character)
        };
    }

    private static async Task WithSource(string source, Func<SourceCompletionHandler, Uri, Task> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing",
            "IncompleteCompletion-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

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
}