
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Diagnostics;
using Sushi.LanguageServerProtocol;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Locks down source-backed semantic-token roles, declaration modifiers,
/// lexical scope boundaries, and UTF-16 coordinates used by the LSP.
/// </summary>
[TestFixture]
public class SourceSemanticTokenClassifierTests
{
    private static readonly Uri testUri = new("file:///SushiTesting/SemanticTokens.sus");

    [TestCase(TestName = "Package Function Parameters And Local References Receive Distinct Semantic Roles")]
    public void FunctionSymbolsShouldBeClassified()
    {
        IReadOnlyList<SourceSemanticToken> tokens = Classify("""
            package Example;
            namespace Example;

            public int32 Compute(int32 input)
            {
                int32 value = input;
                return value;
            }

            public int32 Call()
            {
                return Compute(2);
            }
            """);

        AssertTokenCount(tokens, "Compute", SemanticTokenType.Function, declaration: true, expected: 1);
        AssertTokenCount(tokens, "Compute", SemanticTokenType.Function, declaration: false, expected: 1);
        AssertTokenCount(tokens, "Call", SemanticTokenType.Function, declaration: true, expected: 1);
        AssertTokenCount(tokens, "input", SemanticTokenType.Parameter, declaration: true, expected: 1);
        AssertTokenCount(tokens, "input", SemanticTokenType.Parameter, declaration: false, expected: 1);
        AssertTokenCount(tokens, "value", SemanticTokenType.Variable, declaration: true, expected: 1);
        AssertTokenCount(tokens, "value", SemanticTokenType.Variable, declaration: false, expected: 1);
        AssertTokenCount(tokens, "int32", SemanticTokenType.Type, declaration: false, expected: 4);
        AssertTokenCount(tokens, "Example", SemanticTokenType.Namespace, declaration: true, expected: 2);
    }

    [TestCase(TestName = "Activity Methods And Local Update Targets Receive Correct Symbol Roles")]
    public void ActivitySymbolsShouldBeClassified()
    {
        IReadOnlyList<SourceSemanticToken> tokens = Classify("""
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start()
                {
                    int32 value = 1;
                    ++(value);
                    value++;
                }
            }
            """);

        AssertTokenCount(tokens, "Application", SemanticTokenType.Class, declaration: true, expected: 1);
        AssertTokenCount(tokens, "Start", SemanticTokenType.Method, declaration: true, expected: 1);
        AssertTokenCount(tokens, "value", SemanticTokenType.Variable, declaration: true, expected: 1);
        AssertTokenCount(tokens, "value", SemanticTokenType.Variable, declaration: false, expected: 2);
    }

    [TestCase(TestName = "For Initializer Scope Does Not Escape And Unknown Identifiers Are Not Classified")]
    public void LoopScopeShouldNotLeakSemanticSymbols()
    {
        const string source = """
            package Example;
            namespace Example;

            public void Evaluate()
            {
                for (int32 index = 0; index < 2; index++)
                {
                    int32 value = index;
                }

                index++;
                Missing(index);
            }
            """;

        int outsideLoopOffset = Encoding.UTF8.GetByteCount(source.AsSpan(0, source.LastIndexOf("index++;", StringComparison.Ordinal)));

        IReadOnlyList<SourceSemanticToken> tokens = Classify(source);

        tokens.Should().NotContain(token => token.Span.Start == outsideLoopOffset);

        AssertTokenCount(tokens, "index", SemanticTokenType.Variable, declaration: true, expected: 1);
        AssertTokenCount(tokens, "index", SemanticTokenType.Variable, declaration: false, expected: 3);
        AssertTokenCount(tokens, "value", SemanticTokenType.Variable, declaration: true, expected: 1);

        tokens
            .Should()
            .NotContain(token => GetText(token) == "Missing");
    }

    [TestCase(TestName = "Escaped Parameter Names Preserve Their Source Spelling")]
    public void EscapedIdentifiersShouldRetainCorrectSemanticSpans()
    {
        IReadOnlyList<SourceSemanticToken> tokens = Classify("""
            package Example;
            namespace Example;

            public int32 Evaluate(int32 @stop)
            {
                return @stop;
            }
            """);

        AssertTokenCount(tokens, "@stop", SemanticTokenType.Parameter, declaration: true, expected: 1);
        AssertTokenCount(tokens, "@stop", SemanticTokenType.Parameter, declaration: false, expected: 1);
    }

    [TestCase(TestName = "Named Argument Labels Use Parameter Role")]
    public void NamedArgumentsShouldBeClassified()
    {
        IReadOnlyList<SourceSemanticToken> tokens = Classify("""
            package Example;
            namespace Example;

            public int32 Subtract(int32 left, int32 right)
            {
                return left - right;
            }

            public int32 Evaluate()
            {
                return Subtract(right: 2, left: 10);
            }
            """);

        AssertTokenCount(tokens, "right", SemanticTokenType.Parameter, declaration: true, expected: 1);
        AssertTokenCount(tokens, "right", SemanticTokenType.Parameter, declaration: false, expected: 2);
        AssertTokenCount(tokens, "left", SemanticTokenType.Parameter, declaration: true, expected: 1);
        AssertTokenCount(tokens, "left", SemanticTokenType.Parameter, declaration: false, expected: 2);
    }

    [TestCase(TestName = "Semantic Token Coordinates Use UTF16 Instead Of UTF8 Byte Offsets")]
    public void SemanticTokenCoordinatesShouldBeUtf16Correct()
    {
        const string source = """
            package Example;
            namespace Example;

            public void Evaluate()
            {
                /* 🧪 */ int32 value = 1;
                value++;
            }
            """;

        IReadOnlyList<SourceSemanticToken> tokens = Classify(source);
        SourceSemanticToken declaration = tokens.Single(token => GetText(token) == "value" && token.IsDeclaration);

        (int line, int character) = declaration.Span.Snapshot.GetUtf16Position(declaration.Span.Start);
        (int endLine, int endCharacter) = declaration.Span.Snapshot.GetUtf16Position(declaration.Span.End);

        string[] lines = source.Split('\n');
        int expectedLine = Array.FindIndex(lines, text => text.Contains("int32 value", StringComparison.Ordinal));
        int expectedCharacter = lines[expectedLine].IndexOf("value", StringComparison.Ordinal);

        line.Should().Be(expectedLine);
        endLine.Should().Be(expectedLine);
        character.Should().Be(expectedCharacter);
        (endCharacter - character).Should().Be(5);

        declaration.Span.Start
            .Should()
            .BeGreaterThan(source.IndexOf("value", StringComparison.Ordinal));
    }

    [TestCase(TestName = "Semantic Tokens Are Returned In Strictly Increasing Source Order")]
    public void SemanticTokenSpansShouldBeOrderedAndNonoverlapping()
    {
        IReadOnlyList<SourceSemanticToken> tokens = Classify("""
            package Example;
            namespace Example;

            public int32 Value(int32 input)
            {
                int32 result = input + 1;
                return result;
            }
            """);

        tokens.Should().NotBeEmpty();

        for (int i = 1; i < tokens.Count; i++)
        {
            tokens[i].Span.Start
                .Should()
                .BeGreaterThanOrEqualTo(tokens[i - 1].Span.End);
        }
    }

    [TestCase(TestName = "Current Editor Snapshot Overrides Saved Disk Snapshot")]
    public void SourceDocumentShouldPreferUnsavedEditorContent()
    {
        SourceSnapshot disk = SourceSnapshot.FromText(testUri, 1, "public int32 Old() { return 1; }");
        SourceDocument document = new(testUri, disk);

        SourceSnapshot editor = SourceSnapshot.FromText(testUri, 2, "public int32 NewValue() { return 2; }");
        document.UpdateEditor(editor);

        document.CurrentSnapshot.Should().BeSameAs(editor);
        document.IsCurrent(disk).Should().BeFalse();
        document.IsCurrent(editor).Should().BeTrue();
    }

    private static IReadOnlyList<SourceSemanticToken> Classify(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnostics = new DiagnosticReporter();
        LexerResult lexicalResult = new SourceLexer().Lex(snapshot, diagnostics, CancellationToken.None);
        ParserResult parsed = SourceParser.ParseSourceFile(lexicalResult, diagnostics, CancellationToken.None);

        SourceFileSyntax syntax = parsed.Tree.Root
            .Should()
            .BeOfType<SourceFileSyntax>()
            .Subject;

        return SourceSemanticTokenClassifier.Classify(syntax, CancellationToken.None);
    }

    private static void AssertTokenCount(IReadOnlyList<SourceSemanticToken> tokens, string text, SemanticTokenType type, bool declaration, int expected)
    {
        tokens
            .Count(token => GetText(token) == text && token.TokenType.Equals(type) && token.IsDeclaration == declaration)
            .Should()
            .Be(expected);
    }

    private static string GetText(SourceSemanticToken token)
    {
        SourceSpan span = token.Span;
        return Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
    }
}