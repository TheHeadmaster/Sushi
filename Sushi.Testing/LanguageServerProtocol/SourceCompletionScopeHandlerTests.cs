using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies parameter and local completion against compiler binding
/// and the lexical scopes established by parsed Sushi statements.
/// </summary>
[TestFixture]
public sealed class SourceCompletionScopeHandlerTests
{
    [Test]
    public async Task FunctionParameterShouldBeAvailableThroughoutItsBody()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 amount) { return amo; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return amo"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("amount");

            item.Kind
                .Should()
                .Be(CompletionItemKind.Variable);

            item.Detail
                .Should()
                .Be("parameter int32 amount");

            item.TextEdit!.TextEdit!.NewText
                .Should()
                .Be("amount");
        });
    }

    [Test]
    public async Task InitializedLocalShouldRemainAvailableWhenFollowingExpressionFailsToBind()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 amount)
            {
                int32 result = amount + 2;
                return res;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return res"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("result");

            item.Detail
                .Should()
                .Be("local int32 result");
        });
    }

    [Test]
    public async Task LocalShouldNotBeVisibleInsideItsOwnInitializer()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                int32 result = res;
                return 1;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "= res"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task LocalDeclaredLaterShouldNotBeVisibleBeforeItsDeclaration()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                return res;
                int32 result = 2;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return res"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task InvalidInitializerShouldNotIntroduceACompletionCandidate()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                int32 result = true;
                return res;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return res"), CancellationToken.None);

            completion.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task ValidLocalShouldRemainAvailableAfterAnUnrelatedBindingError()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                int32 safe = 2;
                int32 invalid = true;
                return saf;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return saf"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("safe");
        });
    }

    [Test]
    public async Task ConditionalLocalShouldNotEscapeItsBlock()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                if (true)
                {
                    int32 branch = 2;
                    return bra;
                }

                return bra;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList inside = await handler.Handle(At(source, uri, "return bra", first: true), CancellationToken.None);
            CompletionList outside = await handler.Handle(At(source, uri, "return bra"), CancellationToken.None);

            inside.Items.Select(item => item.Label)
                .Should()
                .Equal("branch");

            outside.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task ConditionalBranchesShouldNotExposeEachOthersLocals()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                if (true)
                {
                    int32 first = 1;
                    return fir;
                }
                else
                {
                    int32 second = 2;
                    return fir;
                }
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList thenBranch = await handler.Handle(At(source, uri, "return fir", first: true), CancellationToken.None);
            CompletionList elseBranch = await handler.Handle(At(source, uri, "return fir"), CancellationToken.None);

            thenBranch.Items.Select(item => item.Label)
                .Should()
                .Equal("first");

            elseBranch.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task ForInitializerLocalShouldBeVisibleInsideTheLoopButNotAfterIt()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                for (int32 index = 0; index < 3; index++)
                {
                    return ind;
                }

                return ind;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList inside = await handler.Handle(At(source, uri, "return ind", first: true), CancellationToken.None);
            CompletionList outside = await handler.Handle(At(source, uri, "return ind"), CancellationToken.None);

            inside.Items.Select(item => item.Label)
                .Should()
                .Equal("index");

            outside.Items
                .Should()
                .BeEmpty();
        });
    }

    [Test]
    public async Task ForInitializerLocalShouldBeAvailableInTheLoopCondition()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start()
            {
                for (int32 index = 0; index < 3; index++)
                {
                    return 1;
                }

                return 2;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "= 0; ind"), CancellationToken.None);

            completion.Items.Select(item => item.Label)
                .Should()
                .Equal("index");
        });
    }

    [Test]
    public async Task ConflictingLocalShouldNotReplaceAFunctionParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 value)
            {
                int32 value = 1;
                return val;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return val"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Label
                .Should()
                .Be("value");

            item.Detail
                .Should()
                .Be("parameter int32 value");
        });
    }

    [Test]
    public async Task LocalBindingShouldTakePrecedenceOverSameNamedFunctionCompletion()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Calculate() { return 1; }
            public int32 Start()
            {
                int32 Calculate = 2;
                return Calc;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return Calc"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Kind
                .Should()
                .Be(CompletionItemKind.Variable);

            item.Detail
                .Should()
                .Be("local int32 Calculate");
        });
    }

    [Test]
    public async Task BooleanLocalShouldUseItsBoundType()
    {
        const string source = """
            package Example;
            namespace Example;
            public bool Start(bool enabled)
            {
                bool active = enabled;
                return act;
            }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            CompletionList completion = await handler.Handle(At(source, uri, "return act"), CancellationToken.None);
            CompletionItem item = completion.Items.Single();

            item.Detail
                .Should()
                .Be("local bool active");
        });
    }

    [Test]
    public async Task EscapedParameterShouldUseItsSourceSpellingForInsertion()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Start(int32 @return) { return ret; }
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

    private static CompletionParams At(string source, Uri uri, string precedingText, bool first = false)
    {
        int start = first
            ? source.IndexOf(precedingText, StringComparison.Ordinal)
            : source.LastIndexOf(precedingText, StringComparison.Ordinal);

        start
            .Should()
            .BeGreaterThanOrEqualTo(0);

        string prefix = source[..(start + precedingText.Length)];
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
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "CompletionScope-" + Guid.NewGuid().ToString("N"));
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