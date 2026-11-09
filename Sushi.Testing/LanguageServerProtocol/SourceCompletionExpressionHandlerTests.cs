using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies type-constrained completion at empty and partially typed
/// expression positions without changing compiler binding rules.
/// </summary>
[TestFixture]
public sealed class SourceCompletionExpressionHandlerTests
{
    [Test]
    public async Task EmptyReturnExpressionShouldOfferOnlyMatchingValueTypes()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate(int32 value) { return value; }
            public bool IsReady() { return true; }
            public int32 Start(int32 amount, bool enabled)
            {
                int32 result = amount;
                return ;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return "), CancellationToken.None);
            string[] names = [.. completion.Items.Select(item => item.Label)];

            names.Should().Contain("amount");
            names.Should().Contain("result");
            names.Should().Contain("Calculate");
            names.Should().NotContain("enabled");
            names.Should().NotContain("IsReady");

            CompletionItem result = completion.Items.Single(item => item.Label == "result");
            TextEdit edit = result.TextEdit!.TextEdit!;

            edit.NewText.Should().Be("result");
            edit.Range.Start.Should().Be(edit.Range.End);
        });
    }

    [Test]
    public async Task VoidReturnShouldNotOfferValueCompletion()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start(int32 amount)
            {
                return ;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return "), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    [Test]
    public async Task EmptyLocalInitializerShouldUseTheDeclaredType()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 amount, bool enabled)
            {
                int32 previous = amount;
                int32 current = ;
                return previous;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "current = "), CancellationToken.None);
            string[] names = [.. completion.Items.Select(item => item.Label)];

            names.Should().Contain("amount");
            names.Should().Contain("previous");
            names.Should().NotContain("enabled");
            names.Should().NotContain("current");
        });
    }

    [TestCase("total = ")]
    [TestCase("total += ")]
    public async Task EmptyAssignmentValueShouldUseTheWritableLocalsType(string assignment)
    {
        string source = $$"""
            package Example;
            namespace Example;
            public void Start(int32 amount, bool enabled)
            {
                int32 total = amount;
                {{assignment}};
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, assignment), CancellationToken.None);
            string[] names = [.. completion.Items.Select(item => item.Label)];

            names.Should().Contain("total");
            names.Should().Contain("amount");
            names.Should().NotContain("enabled");
        });
    }

    [Test]
    public async Task BooleanAssignmentShouldExcludeIntegerBindings()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start(int32 amount, bool enabled)
            {
                bool active = enabled;
                active = ;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "active = "), CancellationToken.None);
            string[] names = [.. completion.Items.Select(item => item.Label)];

            names.Should().Contain("active");
            names.Should().Contain("enabled");
            names.Should().NotContain("amount");
        });
    }

    [Test]
    public async Task FirstPositionalArgumentShouldUseTheFirstParameterType()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Log(int32 amount, bool verbose) { return; }
            public void Start(int32 amount, bool verbose)
            {
                Log( );
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "Log("), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("amount");
        });
    }

    [Test]
    public async Task SecondPositionalArgumentShouldUseTheSecondParameterType()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Log(int32 amount, bool verbose) { return; }
            public void Start(int32 amount, bool verbose)
            {
                Log(1, );
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "Log(1, "), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("verbose");
        });
    }

    [Test]
    public async Task NamedArgumentShouldUseTheSelectedParametersType()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Log(int32 amount, bool verbose) { return; }
            public void Start(int32 amount, bool verbose)
            {
                Log(verbose: );
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "verbose: "), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("verbose");
        });
    }

    [Test]
    public async Task PrefixedReturnCompletionShouldRespectTheExpectedType()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 amount, bool amountEnabled)
            {
                return amo;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return amo"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("amount");
        });
    }

    [Test]
    public async Task EmptyExpressionPositionShouldNotAppearInsideComments()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 amount)
            {
                return /* pending */ ;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "pend"), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    [Test]
    public async Task WhitespaceAfterCompletedStatementShouldNotOfferExpressionCompletion()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start(int32 amount)
            {
                int32 result = amount;  
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "result = amount; "), CancellationToken.None);

            completion.Items.Should().BeEmpty();
        });
    }

    [Test]
    public async Task UnknownInvocationShouldNotProduceGuessedArgumentCompletions()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start(int32 amount)
            {
                Unknown( );
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "Unknown("), CancellationToken.None);

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
            "ExpressionCompletion-" + Guid.NewGuid().ToString("N"));

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