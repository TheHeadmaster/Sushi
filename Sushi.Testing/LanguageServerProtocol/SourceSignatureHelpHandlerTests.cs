using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

[TestFixture]
public sealed class SourceSignatureHelpHandlerTests
{
    [Test]
    public async Task PositionalArgumentsShouldSelectTheCorrespondingDeclaredParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(10, "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Should().ContainSingle();
            help.Signatures.Single().Label.Should().Be("void Move(int32 x, int32 y)");
            help.ActiveSignature.Should().Be(0);
            help.ActiveParameter.Should().Be(1);
            help.Signatures.Single().Parameters!.Select(parameter => parameter.Label.ToString())
                .Should().Equal("int32 x", "int32 y");
        });
    }

    [Test]
    public async Task NamedArgumentsShouldSelectTheDeclaredParameterRatherThanSourceOrder()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(y: 20, x: 10); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(y: 20, x: "), CancellationToken.None);
            help.Should().NotBeNull();
            help!.ActiveParameter.Should().Be(0);
        });
    }

    [TestCase("Move(x: 20, y: 30)", "Move(x: 20, ", 1)]
    [TestCase("Move(y: 20, x: 30)", "Move(y: 20, ", 0)]
    [TestCase("Move(x: 20, y: 30)", "Move(x: 20, y", 1)]
    [TestCase("Move(y: 20, x: 30)", "Move(y: 20, x", 0)]
    public async Task NamedArgumentEditingShouldHighlightTheOnlyRemainingParameter(string invocation, string prefix, int expectedParameter)
    {
        string source = $$"""
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { {{invocation}}; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, prefix), CancellationToken.None);

            help
                .Should()
                .NotBeNull();

            help!.Signatures.Single().Label
                .Should()
                .Be("void Move(int32 x, int32 y)");

            help.ActiveParameter
                .Should()
                .Be(expectedParameter);
        });
    }

    [Test]
    public async Task MultipleRemainingParametersShouldNotBeGuessedAfterANamedArgument()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y, int32 z) {}
            public void Start() { Move(x: 20, y: 30, z: 40); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(x: 20, "), CancellationToken.None);

            help
                .Should()
                .NotBeNull();

            help!.ActiveParameter
                .Should()
                .BeNull();
        });
    }

    [Test]
    public async Task UnknownParameterNamePrefixShouldNotSelectTheRemainingParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(x: 20, z); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(x: 20, z"), CancellationToken.None);

            help
                .Should()
                .NotBeNull();

            help!.ActiveParameter
                .Should()
                .BeNull();
        });
    }

    [Test]
    public async Task NestedCallsShouldSelectTheInnermostCallable()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Sum(int32 first, int32 second) { return first + second; }
            public int32 Start() { return Sum(3, Sum(4, 5)); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Sum(4, "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("int32 Sum(int32 first, int32 second)");
            help.ActiveParameter.Should().Be(1);
        });
    }

    [Test]
    public async Task ParameterDocumentationShouldComeFromTheSelectedCallable()
    {
        const string source = """
            package Example;
            namespace Example;
            /// Moves an object.
            /// @param x Horizontal destination.
            /// @param y Vertical destination.
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(10, "), CancellationToken.None);

            ParameterInformation[] parameters = [.. help!.Signatures.Single().Parameters!];
            parameters[0].Documentation!.MarkupContent!.Value.Should().Contain("Horizontal destination.");
            parameters[1].Documentation!.MarkupContent!.Value.Should().Contain("Vertical destination.");
        });
    }

    [Test]
    public async Task IncompleteArgumentsShouldRetainUnambiguousSignatureInformation()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, ); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(10, "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("void Move(int32 x, int32 y)");
            help.ActiveParameter.Should().Be(1);
        });
    }

    [TestCase("Move(", 0)]
    [TestCase("Move(10, ", 1)]
    [TestCase("Move(10, 20", 1)]
    public async Task EditingAnInvocationShouldTrackTheCurrentPositionalParameter(string prefix, int expectedParameter)
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, prefix), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("void Move(int32 x, int32 y)");
            help.ActiveParameter.Should().Be(expectedParameter);
        });
    }

    [TestCase("Move(", 0)]
    [TestCase("Move(10, ", 1)]
    public async Task UnterminatedCallsShouldRetainAnUnambiguousSignature(string unfinishedCall, int expectedParameter)
    {
        string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() {
            """ + " " + unfinishedCall;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, unfinishedCall), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("void Move(int32 x, int32 y)");
            help.ActiveParameter.Should().Be(expectedParameter);
        });
    }

    [TestCase("Move(x: 20, ", 1)]
    [TestCase("Move(y: 20, ", 0)]
    public async Task UnterminatedNamedCallsShouldHighlightTheOnlyRemainingParameter(string unfinishedCall, int expectedParameter)
    {
        string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() {
            """ + " " + unfinishedCall;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, unfinishedCall), CancellationToken.None);

            help
                .Should()
                .NotBeNull();

            help!.ActiveParameter
                .Should()
                .Be(expectedParameter);
        });
    }

    [Test]
    public async Task ParenthesizedArgumentsShouldNotAdvanceTheOuterParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 Sum(int32 first, int32 second) { return first + second; }
            public int32 Start() { return Sum((1 + 2), Sum(3, 4)); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? grouped = await handler.Handle(At(source, uri, "Sum((1 + "), CancellationToken.None);
            SignatureHelp? inner = await handler.Handle(At(source, uri, "Sum(3, "), CancellationToken.None);
            SignatureHelp? outer = await handler.Handle(At(source, uri, "Sum(3, 4)"), CancellationToken.None);

            grouped!.ActiveParameter.Should().Be(0);
            inner!.ActiveParameter.Should().Be(1);
            outer!.ActiveParameter.Should().Be(1);
        });
    }

    [Test]
    public async Task CommentsShouldNotCountTheirCommasOrExposeSignatureHelpInsideTheComment()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, /* first, second */ 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? insideComment = await handler.Handle(
                At(source, uri, "Move(10, /* first,"), CancellationToken.None);

            SignatureHelp? afterComment = await handler.Handle(
                At(source, uri, "Move(10, /* first, second */ "), CancellationToken.None);

            insideComment.Should().BeNull();
            afterComment.Should().NotBeNull();
            afterComment!.ActiveParameter.Should().Be(1);
        });
    }

    [Test]
    public async Task DuplicateNamedArgumentShouldNotClaimASecondValidParameterMapping()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(x: 10, x: 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(x: 10, x: "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("void Move(int32 x, int32 y)");
            help.ActiveParameter.Should().BeNull();
        });
    }

    [Test]
    public async Task PositionalArgumentAfterNamedArgumentShouldNotClaimAParameterMapping()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(y: 20, 10); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(y: 20, 10"), CancellationToken.None);

            help
                .Should()
                .NotBeNull();

            help!.ActiveParameter
                .Should()
                .BeNull();
        });
    }

    [Test]
    public async Task IncompleteNamedArgumentShouldSelectItsDeclaredParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(y: 20, x: ); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(y: 20, x: "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.ActiveParameter.Should().Be(0);
        });
    }

    [Test]
    public async Task ZeroParameterCallableShouldHaveNoActiveParameter()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Ping() {}
            public void Start() { Ping(); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Ping("), CancellationToken.None);

            help.Should().NotBeNull();
            help!.Signatures.Single().Label.Should().Be("void Ping()");
            help.ActiveParameter.Should().BeNull();
        });
    }

    [Test]
    public async Task ClosedInvocationShouldNotContinueProvidingSignatureHelp()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(10, 20)"), CancellationToken.None);
            help.Should().BeNull();
        });
    }

    [Test]
    public async Task UnknownNestedCallShouldNotFallBackToTheOuterCallable()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(Unknown(10, 20), 30); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Unknown(10, "), CancellationToken.None);
            help.Should().BeNull();
        });
    }

    [Test]
    public async Task InterruptedInvocationShouldNotLeakIntoTheNextStatement()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { Move(10, ; int32 count = 1; }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "int32 count"), CancellationToken.None);
            help.Should().BeNull();
        });
    }

    [Test]
    public async Task Utf16CursorPositionsShouldResolveAfterMultibyteCommentText()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Move(int32 x, int32 y) {}
            public void Start() { /* 🍣 */ Move(10, 20); }
            """;

        await WithSource(source, async (handler, uri) =>
        {
            SignatureHelp? help = await handler.Handle(At(source, uri, "Move(10, "), CancellationToken.None);

            help.Should().NotBeNull();
            help!.ActiveParameter.Should().Be(1);
        });
    }

    private static SignatureHelpParams At(string source, Uri uri, string precedingText)
    {
        int start = source.LastIndexOf(precedingText, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        string prefix = source[..(start + precedingText.Length)];
        int line = prefix.Count(character => character == '\n');
        int character = prefix.Length - prefix.LastIndexOf('\n') - 1;

        return new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
            Position = new Position(line, character)
        };
    }

    private static async Task WithSource(string source, Func<SourceSignatureHelpHandler, Uri, Task> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "SignatureHelp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, source);

            Uri uri = new(path);
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceSignatureHelpHandler handler = new(workspace);
            await test(handler, uri);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}