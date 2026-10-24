using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class ParserCursorTests
{
    private static readonly Uri testUri = new("file:///TestProject/Cursor.sus");

    [TestCase(TestName = "Cursor Should Skip Every Trivia Kind Without Removing Source Tokens")]
    public void CursorShould_0()
    {
        const string source = " \t/* block */\r\n// line\r\n/// docs\r\nSushi ; \t";
        LexerResult lexerResult = Lex(source);
        ParserCursor cursor = new(lexerResult, CancellationToken.None);

        cursor.Snapshot
            .Should()
            .BeSameAs(lexerResult.Snapshot);

        cursor.Position
            .Should()
            .Be(source.IndexOf("Sushi", StringComparison.Ordinal));

        cursor.TryPeek(out LexToken name)
            .Should()
            .BeTrue();

        name.Type
            .Should()
            .Be(LexTokenType.Identifier);

        cursor.Index
            .Should()
            .Be(0);

        cursor.TryConsumeIdentifier(out LexToken consumedName)
            .Should()
            .BeTrue();

        consumedName
            .Should()
            .Be(name);

        cursor.TryConsumePunctuation((byte)';', out LexToken semicolon)
            .Should()
            .BeTrue();

        semicolon.Type
            .Should()
            .Be(LexTokenType.Punctuation);

        cursor.Position
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);

        lexerResult.Tokens
            .Should()
            .Contain(token => token.Type == LexTokenType.BlockComment)
            .And.Contain(token => token.Type == LexTokenType.LineComment)
            .And.Contain(token => token.Type == LexTokenType.DocumentationLineComment)
            .And.Contain(token => token.Type == LexTokenType.LineTerminator);
    }

    [TestCase(TestName = "Cursor Should Look Ahead Across Trivia Without Advancing")]
    public void CursorShould_1()
    {
        const string source = "Alpha /* comment */ Beta;";
        ParserCursor cursor = CreateCursor(source);

        cursor.TryPeek(out LexToken first)
            .Should()
            .BeTrue();

        cursor.TryPeek(out LexToken second, lookahead: 1)
            .Should()
            .BeTrue();

        cursor.TryPeek(out LexToken third, lookahead: 2)
            .Should()
            .BeTrue();

        cursor.TryPeek(out _, lookahead: 3)
            .Should()
            .BeFalse();

        (first.Span.Start, second.Span.Start, third.Span.Start)
            .Should()
            .Be((0, source.IndexOf("Beta", StringComparison.Ordinal), source.IndexOf(';', StringComparison.Ordinal)));

        cursor.Index
            .Should()
            .Be(0);

        cursor.TryAdvance(out LexToken consumed)
            .Should()
            .BeTrue();

        consumed
            .Should()
            .Be(first);

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next
            .Should()
            .Be(second);
    }

    [TestCase(TestName = "Cursor Should Treat Unknown Recovery Elements As Significant By Default")]
    public void CursorShould_2()
    {
        const string source = "Sushi $ .Text;";
        ParserCursor cursor = CreateCursor(source);

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        int index = cursor.Index;

        cursor.TryPeek(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);

        unknown.Span.Start
            .Should()
            .Be(source.IndexOf('$', StringComparison.Ordinal));

        cursor.TryPeek(out LexToken dot, lookahead: 1)
            .Should()
            .BeTrue();

        dot.Type
            .Should()
            .Be(LexTokenType.Punctuation);

        cursor.Index
            .Should()
            .Be(index);

        cursor.TryAdvance(out LexToken consumed)
            .Should()
            .BeTrue();

        consumed
            .Should()
            .Be(unknown);
    }

    [TestCase(TestName = "Cursor Should Cross Unknown Only When Punctuation Recovery Is Requested")]
    public void CursorShould_3()
    {
        const string source = "Sushi $ .Text;";
        LexerResult lexerResult = Lex(source);
        ParserCursor cursor = new(lexerResult, CancellationToken.None);

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        cursor.TryConsumePunctuation((byte)'.', out _)
            .Should()
            .BeFalse();

        cursor.TryConsumePunctuation((byte)'.', out LexToken dot, skipUnknown: true)
            .Should()
            .BeTrue();

        dot.Span.Start
            .Should()
            .Be(source.IndexOf('.', StringComparison.Ordinal));

        cursor.TryConsumeIdentifier(out LexToken name)
            .Should()
            .BeTrue();

        cursor.TokenTextEquals(name, "Text"u8)
            .Should()
            .BeTrue();

        lexerResult.Tokens
            .Should()
            .ContainSingle(token => token.Type == LexTokenType.Unknown);
    }

    [TestCase(TestName = "Cursor Should Cross Unknown Only When Identifier Recovery Is Requested")]
    public void CursorShould_4()
    {
        ParserCursor cursor = CreateCursor("Sushi $ Text");

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        int index = cursor.Index;

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(index);

        cursor.TryConsumeIdentifier(out LexToken name, skipUnknown: true)
            .Should()
            .BeTrue();

        cursor.TokenTextEquals(name, "Text"u8)
            .Should()
            .BeTrue();
    }

    [TestCase("Sushi ; .Text;", LexTokenType.Punctuation, TestName = "Cursor Should Not Cross Recognized Punctuation During Recovery")]
    [TestCase("Sushi namespace .Text;", LexTokenType.Keyword, TestName = "Cursor Should Not Cross Recognized Keyword During Recovery")]
    public void CursorShould_5([NotNull] string source, LexTokenType expectedType)
    {
        ParserCursor cursor = CreateCursor(source);

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        int index = cursor.Index;

        cursor.TryConsumePunctuation((byte)'.', out _, skipUnknown: true)
            .Should()
            .BeFalse();

        cursor.TryConsumeIdentifier(out _, skipUnknown: true)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(index);

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(expectedType);
    }

    [TestCase(TestName = "Cursor Should Not Advance When Token Consumption Fails")]
    public void CursorShould_6()
    {
        ParserCursor cursor = CreateCursor("Sushi;");

        cursor.TryConsumeKeyword("package"u8, out _)
            .Should()
            .BeFalse();

        cursor.TryConsumePunctuation((byte)';', out _)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(0);

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        int index = cursor.Index;

        cursor.TryConsumePunctuation((byte)'.', out _)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(index);

        cursor.TryPeek(out LexToken semicolon)
            .Should()
            .BeTrue();

        semicolon.Type
            .Should()
            .Be(LexTokenType.Punctuation);
    }

    [TestCase(TestName = "Cursor Should Match Keyword Spelling And Lexical Classification Exactly")]
    public void CursorShould_7()
    {
        ParserCursor cursor = CreateCursor("package Package;");

        cursor.TryConsumeKeyword("package"u8, out LexToken keyword)
            .Should()
            .BeTrue();

        keyword.Type
            .Should()
            .Be(LexTokenType.Keyword);

        cursor.TryConsumeKeyword("package"u8, out _)
            .Should()
            .BeFalse();

        cursor.TryConsumeKeyword("Package"u8, out _)
            .Should()
            .BeFalse();

        cursor.TryConsumeIdentifier(out LexToken name)
            .Should()
            .BeTrue();

        cursor.TokenTextEquals(name, "Package"u8)
            .Should()
            .BeTrue();

        cursor.TokenTextEquals(name, "package"u8)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Cursor Should Consume Escaped Identifiers Without Treating Them As Keywords")]
    public void CursorShould_8()
    {
        ParserCursor cursor = CreateCursor("@return;");

        cursor.TryConsumeKeyword("return"u8, out _)
            .Should()
            .BeFalse();

        cursor.TryConsumeIdentifier(out LexToken name)
            .Should()
            .BeTrue();

        name.Type
            .Should()
            .Be(LexTokenType.EscapedIdentifier);

        cursor.TokenTextEquals(name, "@return"u8)
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Cursor Should Not Accept Unknown Lexical Elements As Punctuation")]
    public void CursorShould_9()
    {
        ParserCursor cursor = CreateCursor("Sushi/Other");

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        int index = cursor.Index;

        cursor.TryConsumePunctuation((byte)'/', out _)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(index);

        cursor.TryPeek(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);
    }

    [TestCase("", TestName = "Cursor Should Report EOF For Empty Source")]
    [TestCase(" \t\r\n/* comment */", TestName = "Cursor Should Report EOF For Trivia Only Source")]
    public void CursorShould_10([NotNull] string source)
    {
        LexerResult lexerResult = Lex(source);
        ParserCursor cursor = new(lexerResult, CancellationToken.None);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        cursor.TryAdvance(out _)
            .Should()
            .BeFalse();

        cursor.Index
            .Should()
            .Be(0);

        cursor.Position
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);
    }

    [TestCase(TestName = "Cursor Should Use UTF8 Byte Offsets And Include Trailing Trivia At EOF")]
    public void CursorShould_11()
    {
        LexerResult lexerResult = Lex("雪 Sushi  ");
        ParserCursor cursor = new(lexerResult, CancellationToken.None);

        cursor.TryAdvance(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);

        unknown.Span.Length
            .Should()
            .Be("雪"u8.Length);

        cursor.Position
            .Should()
            .Be("雪 "u8.Length);

        cursor.TryConsumeIdentifier(out _)
            .Should()
            .BeTrue();

        cursor.Position
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);

        cursor.TryAdvance(out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Cursor Should Honor Cancellation While Scanning Tokens")]
    public void CursorShould_12()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        ParserCursor cursor = new(Lex("Sushi"), cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        Action action = () => cursor.TryPeek(out _);

        action
            .Should()
            .Throw<OperationCanceledException>();
    }

    [TestCase(TestName = "Cursor Should Reject Negative Lookahead Without Advancing")]
    public void CursorShould_13()
    {
        ParserCursor cursor = CreateCursor("Sushi;");
        Action action = () => cursor.TryPeek(out _, lookahead: -1);

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();

        cursor.Index
            .Should()
            .Be(0);
    }

    private static ParserCursor CreateCursor(string source) => new(Lex(source), CancellationToken.None);

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceLexer lexer = new();
        LexerResult result = lexer.Lex(snapshot, diagnosticReporter, CancellationToken.None);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return result;
    }
}