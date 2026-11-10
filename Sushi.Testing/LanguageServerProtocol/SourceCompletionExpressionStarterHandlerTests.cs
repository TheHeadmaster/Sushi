using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies expression starters whose type can become valid through a legal comparison continuation.
/// </summary>
[TestFixture]
public sealed class SourceCompletionExpressionStarterHandlerTests
{
    [TestCase("if (§) { }", "void")]
    [TestCase("if (isAlive) { } else if (§) { }", "void")]
    [TestCase("while (§) { }", "void")]
    [TestCase("do { } while (§);", "void")]
    [TestCase("for (; §; ) { }", "void")]
    [TestCase("return §;", "bool")]
    [TestCase("bool flag = §;", "void")]
    [TestCase("ready = §;", "void")]
    [TestCase("SetReady(§);", "void")]
    [TestCase("SetReady(ready: §);", "void")]
    public async Task BooleanResultContextsShouldOfferDirectAndExtendableStarters(string statement, string returnType)
    {
        CompletionList completion = await Complete(CreateSource(statement, returnType));
        string[] values = GetValueLabels(completion);

        values.Should().Contain(["isAlive", "ready", "BooleanFunction", "health", "amount", "IntegerFunction"]);
        values.Should().NotContain("VoidFunction");
    }

    [Test]
    public async Task DirectBooleanCandidatesShouldRankBeforeIntegerExpressionStarters()
    {
        CompletionList completion = await Complete(CreateSource("if (§) { }", "void"));
        CompletionItem booleanBinding = completion.Items.Single(item => item.Label == "isAlive");
        CompletionItem integerBinding = completion.Items.Single(item => item.Label == "health");
        CompletionItem booleanFunction = completion.Items.Single(item => item.Label == "BooleanFunction");
        CompletionItem integerFunction = completion.Items.Single(item => item.Label == "IntegerFunction");

        booleanBinding.SortText.Should().StartWith("0_");
        booleanFunction.SortText.Should().StartWith("0_");
        integerBinding.SortText.Should().StartWith("1_");
        integerFunction.SortText.Should().StartWith("1_");
    }

    [Test]
    public async Task PartiallyTypedIntegerStarterShouldRespectThePrefixInBooleanContexts()
    {
        CompletionList completion = await Complete(CreateSource("if (hea§) { }", "void"));

        completion.Items.Select(item => item.Label).Should().Equal("health");
        completion.Items.Single().TextEdit!.TextEdit!.NewText.Should().Be("health");
    }

    [TestCase("return health + § == 10;", "bool")]
    [TestCase("if (health < §) { }", "void")]
    [TestCase("if (health == §) { }", "void")]
    [TestCase("if (health + (§) > 10) { }", "void")]
    public async Task EstablishedIntegerOperandsShouldRemainStrict(string statement, string returnType)
    {
        CompletionList completion = await Complete(CreateSource(statement, returnType));
        string[] values = GetValueLabels(completion);

        values.Should().Contain(["health", "amount", "IntegerFunction"]);
        values.Should().NotContain(["isAlive", "ready", "BooleanFunction"]);
    }

    [Test]
    public async Task ImmediateNotOperandShouldRemainBooleanOnly()
    {
        CompletionList completion = await Complete(CreateSource("if (not §) { }", "void"));
        string[] values = GetValueLabels(completion);

        values.Should().Contain(["isAlive", "ready", "BooleanFunction"]);
        values.Should().NotContain(["health", "amount", "IntegerFunction"]);
    }

    [TestCase("if (not (§)) { }", "void")]
    [TestCase("if (isAlive and §) { }", "void")]
    [TestCase("if (isAlive or §) { }", "void")]
    [TestCase("if (isAlive & (§)) { }", "void")]
    public async Task GroupingAndLogicalPrecedenceShouldAllowIntegerComparisonStarters(string statement, string returnType)
    {
        CompletionList completion = await Complete(CreateSource(statement, returnType));
        string[] values = GetValueLabels(completion);

        values.Should().Contain(["isAlive", "BooleanFunction", "health", "IntegerFunction"]);
    }

    [Test]
    public async Task BooleanEagerBitwiseOperandShouldNotUseAnUnparenthesizedIntegerStarter()
    {
        CompletionList completion = await Complete(CreateSource("if (isAlive & §) { }", "void"));
        string[] values = GetValueLabels(completion);

        values.Should().Contain(["isAlive", "BooleanFunction"]);
        values.Should().NotContain(["health", "amount", "IntegerFunction"]);
    }

    [Test]
    public async Task BooleanNamedArgumentShouldRetainItsLabelAndAllowBothValueStarterTypes()
    {
        CompletionList completion = await Complete(CreateSource("SetReady(§);", "void"));

        completion.Items.Should().Contain(item => item.Kind == CompletionItemKind.Property && item.Label == "ready:");
        completion.Items.Should().Contain(item => item.Kind == CompletionItemKind.Variable && item.Label == "isAlive");
        completion.Items.Should().Contain(item => item.Kind == CompletionItemKind.Variable && item.Label == "health");
        completion.Items.Should().Contain(item => item.Kind == CompletionItemKind.Function && item.Label == "IntegerFunction");

        CompletionList namedValue = await Complete(CreateSource("SetReady(ready: §);", "void"));
        namedValue.Items.Should().NotContain(item => item.Kind == CompletionItemKind.Property);
        GetValueLabels(namedValue).Should().Contain(["isAlive", "health", "IntegerFunction"]);
    }

    [Test]
    public async Task AmbiguousExpressionStatementBitwiseOperandShouldNotGuessAType()
    {
        CompletionList completion = await Complete(CreateSource("UnknownValue & §;", "void"));

        completion.Items.Should().BeEmpty();
    }

    private static string[] GetValueLabels(CompletionList completion)
        => [.. completion.Items
            .Where(item => item.Kind != CompletionItemKind.Property)
            .Select(item => item.FilterText ?? item.Label)];

    private static string CreateSource(string statement, string returnType)
        => $$"""
            package Example;
            namespace Example;
            public bool BooleanFunction() { return true; }
            public int32 IntegerFunction() { return 1; }
            public void VoidFunction() { return; }
            public void SetReady(bool ready) { return; }
            public {{returnType}} Start(int32 health, bool isAlive)
            {
                int32 amount = health;
                bool ready = isAlive;
                {{statement}}
            }
            """;

    private static async Task<CompletionList> Complete(string sourceWithCursor)
    {
        const string marker = "§";
        int markerIndex = sourceWithCursor.IndexOf(marker, StringComparison.Ordinal);
        markerIndex.Should().BeGreaterThanOrEqualTo(0);

        string source = sourceWithCursor.Remove(markerIndex, marker.Length);
        string precedingText = sourceWithCursor[..markerIndex];
        int line = precedingText.Count(character => character == '\n');
        int character = precedingText.Length - precedingText.LastIndexOf('\n') - 1;

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing",
            "ExpressionStarterCompletion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, source);
            Uri uri = new(path);

            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            return await handler.Handle(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
                Position = new Position(line, character)
            }, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
