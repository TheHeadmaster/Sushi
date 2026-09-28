using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Testing.Lexing;

[TestFixture]
public class SourceLexerTests
{
    private static readonly Uri testUri = new("file:///TestProject/Test.sus");

    [TestCase(TestName = "Lex Should Produce No Tokens For Empty Source")]
    public void LexShould_0()
    {
        LexerResult result = Lex(string.Empty);

        result.Tokens
            .Should()
            .BeEmpty();

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Group Contiguous Horizontal Whitespace")]
    public void LexShould_1()
    {
        LexerResult result = Lex(" \t\t ");

        AssertTokens(result, (LexTokenType.Whitespace, 0, 4));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Recognize Each Supported Line Terminator")]
    public void LexShould_2()
    {
        LexerResult result = Lex("\n\r\r\n");

        AssertTokens(result,
            (LexTokenType.LineTerminator, 0, 1),
            (LexTokenType.LineTerminator, 1, 2),
            (LexTokenType.LineTerminator, 2, 4));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Leave Line Terminator Outside Line Comment")]
    public void LexShould_3()
    {
        LexerResult result = Lex("// comment\r\n");

        AssertTokens(result, (LexTokenType.LineComment, 0, 10), (LexTokenType.LineTerminator, 10, 12));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Recognize Documentation Line Comment")]
    public void LexShould_4()
    {
        LexerResult result = Lex("/// documentation");

        AssertTokens(result, (LexTokenType.DocumentationLineComment, 0, 17));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Recognize Nested Block Comment As One Token")]
    public void LexShould_5()
    {
        const string source = "/* outer /* inner */ outer */";

        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.BlockComment, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Ignore Line Comment Syntax Inside Block Comment")]
    public void LexShould_6()
    {
        const string source = "/* // not a line comment\n/* nested */ end */";

        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.BlockComment, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Preserve Unterminated Block Comment And Produce Diagnostic")]
    public void LexShould_7()
    {
        const string source = "/* never closed";

        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.BlockComment, 0, source.Length));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);

        diagnostic.Span.Start
            .Should()
            .Be(0);

        diagnostic.Span.End
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Lex Should Group Contiguous Unrecognized Source Into Unknown Token")]
    public void LexShould_8()
    {
        LexerResult result = Lex("ня   foo");

        AssertTokens(result, 
            (LexTokenType.Unknown, 0, 4),
            (LexTokenType.Whitespace, 4, 7),
            (LexTokenType.Identifier, 7, 10));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Keep Non Comment Slash Inside Unknown Run")]
    public void LexShould_9()
    {
        LexerResult result = Lex("abc/def ");

        AssertTokens(result, 
            (LexTokenType.Identifier, 0, 3),
            (LexTokenType.Unknown, 3, 4),
            (LexTokenType.Identifier, 4, 7),
            (LexTokenType.Whitespace, 7, 8));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Partition Entire Source Without Gaps Or Overlaps")]
    public void LexShould_10()
    {
        const string source = "abc\t// line\r\n/* outer /* inner */ outer */ xyz";

        LexerResult result = Lex(source);

        result.Tokens
            .Select(token => token.Type)
            .Should()
            .Equal(
                LexTokenType.Identifier,
                LexTokenType.Whitespace,
                LexTokenType.LineComment,
                LexTokenType.LineTerminator,
                LexTokenType.BlockComment,
                LexTokenType.Whitespace,
                LexTokenType.Identifier
            );
            
        AssertTokensPartitionSource(result);

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Carve Malformed UTF-8 Out Of Line Comment")]
    public void LexShould_11()
    {
        byte[] source =
        [
            .. "// abc "u8,
            0xFF,
            .. " def\r\n"u8
        ];

        LexerResult result = Lex(source);

        AssertTokens(result,
            (LexTokenType.LineComment, 0, 7),
            (LexTokenType.Unknown, 7, 8),
            (LexTokenType.LineComment, 8, 12),
            (LexTokenType.LineTerminator, 12, 14)
        );

        AssertTokensPartitionSource(result);

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Carve Malformed UTF-8 Out Of Block Comment")]
    public void LexShould_12()
    {
        byte[] source =
        [
            .. "/* abc "u8,
            0xFF,
            .. " def */"u8
        ];

        LexerResult result = Lex(source);

        AssertTokens(result,
            (LexTokenType.BlockComment, 0, 7),
            (LexTokenType.Unknown, 7, 8),
            (LexTokenType.BlockComment, 8, 15)
        );

        AssertTokensPartitionSource(result);

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Keep Malformed UTF-8 Separate From Adjacent Unknown Source")]
    public void LexShould_13()
    {
        byte[] source =
        [
            .. "abc"u8,
            0xFF,
            .. "def "u8
        ];

        LexerResult result = Lex(source);

        AssertTokens(result,
            (LexTokenType.Identifier, 0, 3),
            (LexTokenType.Unknown, 3, 4),
            (LexTokenType.Identifier, 4, 7),
            (LexTokenType.Whitespace, 7, 8)
        );

        AssertTokensPartitionSource(result);

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        SourceLexer lexer = new();

        return lexer.Lex(snapshot, CancellationToken.None);
    }

    private static LexerResult Lex(ReadOnlySpan<byte> source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        SourceLexer lexer = new();

        return lexer.Lex(snapshot, CancellationToken.None);
    }

    private static void AssertTokens(LexerResult result, params (LexTokenType Type, int Start, int End)[] expected)
    {
        result.Tokens
            .Should()
            .HaveCount(expected.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            LexToken token = result.Tokens[i];

            (LexTokenType Type, int Start, int End) expectation = expected[i];

            token.Type
                .Should()
                .Be(expectation.Type);

            token.Span.Snapshot
                .Should()
                .BeSameAs(result.Snapshot);

            token.Span.Start
                .Should()
                .Be(expectation.Start);

            token.Span.End
                .Should()
                .Be(expectation.End);
        }
    }

    private static void AssertTokensPartitionSource(LexerResult result)
    {
        int position = 0;

        foreach (LexToken token in result.Tokens)
        {
            token.Span.Snapshot
                .Should()
                .BeSameAs(result.Snapshot);

            token.Span.Start
                .Should()
                .Be(position);

            token.Span.Length
                .Should()
                .BeGreaterThan(0);

            position = token.Span.End;
        }

        position
            .Should()
            .Be(result.Snapshot.SourceLength);
    }
}