using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies that completion propagates expected types through nested source expressions.
/// </summary>
[TestFixture]
public sealed class SourceCompletionNestedExpressionHandlerTests
{
    private const string CursorMarker = "§cursor§";

    [TestCase("if (/*cursor*/) { }")]
    [TestCase("if (enabled) { } else if (/*cursor*/) { }")]
    [TestCase("while (/*cursor*/) { }")]
    [TestCase("do { } while (/*cursor*/)")]
    [TestCase("for (int32 index = 0;/*cursor*/; index++) { }")]
    public async Task BooleanControlConditionsShouldOfferOnlyBooleanValues(string statement)
    {
        await AssertCompletion(
            CreateSource("int32", statement),
            ["enabled", "active", "BooleanFunction"],
            ["amount", "total", "IntegerFunction"]);
    }

    [Test]
    public async Task EmptyForConditionShouldExcludeTheVisibleIntegerLoopVariable()
    {
        await AssertCompletion(
            CreateSource("int32", "for (int32 index = 0;/*cursor*/; index++) { }"),
            ["enabled", "active", "BooleanFunction"],
            ["amount", "total", "index", "IntegerFunction"]);
    }

    [TestCase("return (/*cursor*/);", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return ((/*cursor*/));", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return (((/*cursor*/)));", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return ((/*cursor*/));", "bool", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    public async Task ParenthesizedReturnExpressionsShouldInheritTheFunctionReturnType(
        string expression, string returnType, string include, string includeFunction, string exclude, string excludeFunction)
    {
        await AssertCompletion(
            CreateSource(returnType, expression),
            [include, "total", includeFunction],
            [exclude, "active", excludeFunction]);
    }

    [TestCase("return amount + /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount + am/*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount - /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount * /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount / /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount % /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount << /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount >> /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return +/*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return -/*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return ~/*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (not /*cursor*/) { }", "int32", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    [TestCase("if (enabled and /*cursor*/) { }", "int32", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    [TestCase("if (enabled or /*cursor*/) { }", "int32", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    [TestCase("if (amount < /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (amount == /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (amount != /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (amount <= /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (amount > /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("if (amount >= /*cursor*/) { }", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    public async Task UnaryArithmeticLogicalAndComparisonOperandsShouldUseTheirOperandTypes(
        string expression, string returnType, string include, string includeFunction, string exclude, string excludeFunction)
    {
        await AssertCompletion(
            CreateSource(returnType, expression),
            [include, includeFunction],
            [exclude, excludeFunction]);
    }

    [TestCase("return enabled & /*cursor*/;", "bool", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    [TestCase("return amount & /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return amount | /*cursor*/;", "int32", "amount", "IntegerFunction", "enabled", "BooleanFunction")]
    [TestCase("return enabled ^ /*cursor*/;", "bool", "enabled", "BooleanFunction", "amount", "IntegerFunction")]
    public async Task EagerBitwiseOperandsShouldUseAConsistentEstablishedType(
        string expression, string returnType, string include, string includeFunction, string exclude, string excludeFunction)
    {
        await AssertCompletion(
            CreateSource(returnType, expression),
            [include, includeFunction],
            [exclude, excludeFunction]);
    }

    [Test]
    public async Task AmbiguousBitwiseOperandShouldNotGuessABooleanOrIntegerType()
    {
        await AssertCompletion(
            CreateSource("int32", "UnknownValue & /*cursor*/;"),
            [],
            ["amount", "enabled", "IntegerFunction", "BooleanFunction"]);
    }

    [Test]
    public async Task NestedPositionalAndNamedInvocationArgumentsShouldKeepTheirOwnParameterTypes()
    {
        await AssertCompletion(
            CreateSource("int32", "ConsumeBoolean((/*cursor*/));"),
            ["enabled", "active", "BooleanFunction"],
            ["amount", "IntegerFunction"]);

        await AssertCompletion(
            CreateSource("int32", "ConsumeNamed(ready: (/*cursor*/));"),
            ["enabled", "active", "BooleanFunction"],
            ["amount", "IntegerFunction"]);
    }

    [Test]
    public async Task ArithmeticInsideAnInvocationArgumentShouldUseIntegerOperandType()
    {
        await AssertCompletion(
            CreateSource("int32", "ConsumeInteger(amount + /*cursor*/);"),
            ["amount", "total", "IntegerFunction"],
            ["enabled", "BooleanFunction"]);
    }

    [Test]
    public async Task UnresolvedNestedInvocationShouldNotInheritItsOuterArgumentType()
    {
        await AssertCompletion(
            CreateSource("int32", "ConsumeBoolean(UnknownFunction(/*cursor*/));"),
            [],
            ["amount", "enabled", "IntegerFunction", "BooleanFunction"]);
    }

    [Test]
    public async Task IncompleteGroupingShouldRetainItsReturnTypeWithoutInventingClosingSyntax()
    {
        await AssertCompletion(
            CreateSource("int32", "return ((/*cursor*/;"),
            ["amount", "total", "IntegerFunction"],
            ["enabled", "BooleanFunction"]);
    }

    [Test]
    public async Task CompletionShouldRemainSuppressedInCommentsAfterCompletedConditionsAndInVoidReturns()
    {
        await AssertCompletion(
            CreateSource("int32", "int32 result = amount;/*cursor*/ return amount;"),
            [],
            ["amount", "IntegerFunction"]);

        await AssertCompletion(
            CreateSource("int32", "return /*cursor*/; // comment"),
            [],
            ["amount", "IntegerFunction"]);

        await AssertCompletion(
            CreateSource("void", "return /*cursor*/;"),
            [],
            ["amount", "IntegerFunction"]);

        await AssertCompletion(
            CreateSource("int32", "if (enabled) /*cursor*/ { } return amount;"),
            [],
            ["amount", "IntegerFunction"]);
    }

    [Test]
    public async Task CompletionShouldNotExposeBindingsFromAnotherFunction()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 IntegerFunction(int32 value) { return value; }
            public int32 First(int32 hiddenAmount) { return hiddenAmount; }
            public int32 Second(bool enabled)
            {
                return /*cursor*/;
            }
            """;

        await AssertCompletion(source, ["enabled"], ["hiddenAmount"]);
    }

    private static string CreateSource(string returnType, string statement)
        => $$"""
            package Example;
            namespace Example;
            public int32 IntegerFunction(int32 value) { return value; }
            public bool BooleanFunction(bool value) { return value; }
            public void ConsumeBoolean(bool ready) { return; }
            public void ConsumeNamed(bool ready) { return; }
            public void ConsumeInteger(int32 value) { return; }
            public {{returnType}} Start(int32 amount, bool enabled)
            {
                int32 total = amount;
                bool active = enabled;
                {{statement}}
            }
            """;

    private static async Task AssertCompletion(
        string sourceWithMarker, IReadOnlyCollection<string> expected, IReadOnlyCollection<string> excluded)
    {
        string marker = sourceWithMarker.Contains("/*cursor*/", StringComparison.Ordinal) ? "/*cursor*/" : CursorMarker;
        int markerIndex = sourceWithMarker.IndexOf(marker, StringComparison.Ordinal);
        markerIndex.Should().BeGreaterThanOrEqualTo(0);

        string source = sourceWithMarker.Remove(markerIndex, marker.Length);
        string prefix = sourceWithMarker[..markerIndex];
        int line = prefix.Count(character => character == '\n');
        int character = prefix.Length - prefix.LastIndexOf('\n') - 1;

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing",
            "NestedExpressionCompletion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "Program.sus");
            await File.WriteAllTextAsync(path, source);
            Uri uri = new(path);

            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);

            SourceCompletionHandler handler = new(workspace);
            CompletionList completion = await handler.Handle(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From(uri)),
                Position = new Position(line, character)
            }, CancellationToken.None);

            string[] labels = [.. completion.Items.Select(item => item.FilterText ?? item.Label)];
            labels.Should().Contain(expected);
            labels.Should().NotContain(excluded);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
