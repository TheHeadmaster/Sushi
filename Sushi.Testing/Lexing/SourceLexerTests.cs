using System.Diagnostics.CodeAnalysis;
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

    [TestCase("foo", LexTokenType.Identifier, TestName = "Lex Should Recognize Ordinary Identifier")]
    [TestCase("foo123", LexTokenType.Identifier, TestName = "Lex Should Recognize Identifier Containing Digits")]
    [TestCase("_value", LexTokenType.Identifier, TestName = "Lex Should Recognize Identifier Beginning With Underscore")]
    [TestCase("__internalThing", LexTokenType.Identifier, TestName = "Lex Should Recognize Identifier Beginning With Multiple Underscores")]
    [TestCase("value_", LexTokenType.Identifier, TestName = "Lex Should Recognize Identifier Ending With Underscore")]
    [TestCase("int32", LexTokenType.Identifier, TestName = "Lex Should Classify Built-In Integer Type Name As Identifier")]
    [TestCase("returnValue", LexTokenType.Identifier, TestName = "Lex Should Not Shorten Identifier To Keyword Prefix")]
    [TestCase("Return", LexTokenType.Identifier, TestName = "Lex Should Recognize Keywords Case Sensitively")]
    [TestCase("True", LexTokenType.Identifier, TestName = "Lex Should Recognize Boolean Literals Case Sensitively")]
    [TestCase("ifelse", LexTokenType.Identifier, TestName = "Lex Should Preserve Maximal Munch Across Keyword Shaped Identifier")]
    [TestCase("true42", LexTokenType.Identifier, TestName = "Lex Should Preserve Maximal Munch Across Boolean Shaped Identifier")]
    public void LexShould_14([NotNull] string source, LexTokenType expectedType)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (expectedType, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true", TestName = "Lex Should Recognize True Boolean Literal")]
    [TestCase("false", TestName = "Lex Should Recognize False Boolean Literal")]
    public void LexShould_15([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.BooleanLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("@return", TestName = "Lex Should Recognize Escaped Keyword Identifier")]
    [TestCase("@true", TestName = "Lex Should Recognize Escaped Boolean Identifier")]
    [TestCase("@int32", TestName = "Lex Should Recognize Escaped Built-In Type Identifier")]
    public void LexShould_16([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.EscapedIdentifier, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Not Recognize Escape For Unreserved Identifier")]
    public void LexShould_17()
    {
        LexerResult result = Lex("@foo");

        AssertTokens(result, (LexTokenType.Unknown, 0, 1), (LexTokenType.Identifier, 1, 4));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Not Repair Invalid Identifier With Escape Prefix")]
    public void LexShould_18()
    {
        LexerResult result = Lex("@123abc");

        AssertTokens(result,
            (LexTokenType.Unknown, 0, 1),
            (LexTokenType.IntegerLiteral, 1, 4),
            (LexTokenType.Identifier, 4, 7));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(4);

        diagnostic.Span.End
            .Should()
            .Be(4);
    }

    [TestCase("_", TestName = "Lex Should Not Recognize Single Underscore As Identifier")]
    [TestCase("__", TestName = "Lex Should Not Recognize Multiple Underscores As Identifier")]
    [TestCase("___", TestName = "Lex Should Not Recognize All Underscore Sequence As Identifier")]
    public void LexShould_19([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.Unknown, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCaseSource(nameof(KeywordCases))]
    public void LexShould_20([NotNull] string keyword)
    {
        LexerResult result = Lex(keyword);

        AssertTokens(result, (LexTokenType.Keyword, 0, keyword.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCaseSource(nameof(ClassifyBuiltInIntegerTypeCases))]
    public void LexShould_21([NotNull] string typeName)
    {
        LexerResult result = Lex(typeName);

        AssertTokens(result, (LexTokenType.Identifier, 0, typeName.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
        
    }

    [TestCaseSource(nameof(EscapeBuiltInIntegerTypeCases))]
    public void LexShould_22([NotNull] string typeName)
    {
        string source = $"@{typeName}";

        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.EscapedIdentifier, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("0", TestName = "Lex Should Recognize Single Character Integer Literal")]
    [TestCase("42", TestName = "Lex Should Recognize Multiple Character Integer Literal")]
    [TestCase("00042", TestName = "Lex Should Recognize Integer Literal With Leading Zeroes")]
    [TestCase("000_042", TestName = "Lex Should Recognize Integer Literal With Single Separator")]
    [TestCase("1_000_000", TestName = "Lex Should Recognize Integer Literal With Multiple Separators")]
    [TestCase("b#0", TestName = "Lex Should Recognize Radix Prefixed Binary Literal")]
    [TestCase("b#1111_0000", TestName = "Lex Should Recognize Radix Prefixed Binary Literal With Single Separator")]
    [TestCase("o#755_644", TestName = "Lex Should Recognize Radix Prefixed Octal Literal")]
    [TestCase("x#dead_beef", TestName = "Lex Should Recognize Radix Prefixed Hexadecimal Literal")]
    [TestCase("x#DEAD_BEEF", TestName = "Lex Should Recognize Radix Prefixed Hexadecimal Literal With Uppercase")]
    [TestCase("x#DeAd_BeEf", TestName = "Lex Should Recognize Radix Prefixed Hexadecimal Literal Case Insensitively")]
    public void LexShould_23([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("b#", TestName = "Lex Should Reject Binary Literal Prefix With No Numeric Component")]
    [TestCase("o#", TestName = "Lex Should Reject Octal Literal Prefix With No Numeric Component")]
    [TestCase("x#", TestName = "Lex Should Reject Hexadecimal Literal Prefix With No Numeric Component")]
    public void LexShould_24([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(0);

        diagnostic.Span.End
            .Should()
            .Be(2);
    }

    [TestCase("b#2", 2, TestName = "Lex Should Reject Binary Literal With Non Binary Digit")]
    [TestCase("b#102", 4, TestName = "Lex Should Reject Binary Literal With Non Binary Digit In Any Position")]
    [TestCase("o#8", 2, TestName = "Lex Should Reject Octal Literal With Non Octal Digit")]
    [TestCase("o#758", 4, TestName = "Lex Should Reject Octal Literal With Non Octal Digit In Any Position")]
    public void LexShould_25([NotNull] string source, int invalidDigitPosition)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(invalidDigitPosition);

        diagnostic.Span.End
            .Should()
            .Be(invalidDigitPosition + 1);
    }

    [TestCase("b#_1010", 2, TestName = "Lex Should Reject Binary Literal With Primal Separator")]
    [TestCase("b#1010_", 6, TestName = "Lex Should Reject Binary Literal With Terminal Separator")]
    [TestCase("o#_755", 2, TestName = "Lex Should Reject Octal Literal With Primal Separator")]
    [TestCase("o#755_", 5, TestName = "Lex Should Reject Octal Literal With Terminal Separator")]
    [TestCase("x#_9f", 2, TestName = "Lex Should Reject Hexadecimal Literal With Primal Separator")]
    [TestCase("x#9f_", 4, TestName = "Lex Should Reject Hexadecimal Literal With Terminal Separator")]
    public void LexShould_26([NotNull] string source, int invalidSeparatorPosition)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(invalidSeparatorPosition);

        diagnostic.Span.End
            .Should()
            .Be(invalidSeparatorPosition + 1);
    }

    [TestCase(TestName = "Lex Should Preserve Consecutive Invalid Radix Separators Inside Integer Literal")]
    public void LexShould_27()
    {
        const string source = "x#dead__beef";

        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, source.Length));

        result.Diagnostics
            .Should()
            .HaveCount(2);

        result.Diagnostics
            .Select(diagnostic => (diagnostic.Span.Start, diagnostic.Span.End))
            .Should()
            .Equal((6, 7), (7, 8));
    }

    [TestCase(TestName = "Lex Should Diagnose Adjacent Hexadecimal Literal And Identifier")]
    public void LexShould_28()
    {
        LexerResult result = Lex("x#12g");

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, 4), (LexTokenType.Identifier, 4, 5));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(4);

        diagnostic.Span.End
            .Should()
            .Be(4);
    }

    [TestCase(TestName = "Lex Should Diagnose Adjacent Binary Literal And Identifier")]
    public void LexShould_29()
    {
        LexerResult result = Lex("b#10cat");

        AssertTokens(result,
            (LexTokenType.IntegerLiteral, 0, 4),
            (LexTokenType.Identifier, 4, 7));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(4);

        diagnostic.Span.End
            .Should()
            .Be(4);
    }

    [TestCase(TestName = "Lex Should Not Recognize C Style Binary Prefix")]
    public void LexShould_30()
    {
        LexerResult result = Lex("0b101");

        AssertTokens(result,
            (LexTokenType.IntegerLiteral, 0, 1),
            (LexTokenType.Identifier, 1, 5));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(1);

        diagnostic.Span.End
            .Should()
            .Be(1);
    }

    [TestCase(TestName = "Lex Should Recognize Radix Prefixes Case Sensitively")]
    public void LexShould_31()
    {
        LexerResult result = Lex("B#101");

        AssertTokens(result,
            (LexTokenType.Identifier, 0, 1),
            (LexTokenType.Unknown, 1, 2),
            (LexTokenType.IntegerLiteral, 2, 5));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Suppress Adjacency Diagnostic When Missing Radix Digit Explains Boundary")]
    public void LexShould_32()
    {
        LexerResult result = Lex("b#foo");

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, 2), (LexTokenType.Identifier, 2, 5));

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(0);

        diagnostic.Span.End
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Lex Should Preserve Adjacency Diagnostic When Existing Error Does Not Explain Boundary")]
    public void LexShould_33()
    {
        LexerResult result = Lex("b#102foo");

        AssertTokens(result, (LexTokenType.IntegerLiteral, 0, 5), (LexTokenType.Identifier, 5, 8));

        result.Diagnostics
            .Should()
            .HaveCount(2);

        result.Diagnostics
            .Select(diagnostic => (diagnostic.Span.Start, diagnostic.Span.End))
            .Should()
            .Equal(
                (4, 5),
                (5, 5));
    }

    [TestCase("42foo", 2, TestName = "Lex Should Reject Adjacent Integer Literal And Identifier")]
    [TestCase("42true", 2, TestName = "Lex Should Reject Adjacent Integer Literal And Boolean Literal")]
    [TestCase("42@return", 2, TestName = "Lex Should Reject Adjacent Integer Literal And Escaped Identifier")]
    [TestCase("b#10x#20", 4, TestName = "Lex Should Reject Adjacent Integer Literals")]
    [TestCase("foo@return", 3, TestName = "Lex Should Reject Adjacent Identifier And Escaped Identifier")]
    [TestCase("return@public", 6, TestName = "Lex Should Reject Adjacent Keyword And Escaped Identifier")]
    [TestCase("true@false", 4, TestName = "Lex Should Reject Adjacent Boolean Literal And Escaped Identifier")]
    [TestCase("42i32", 2, TestName = "Lex Should Reject Integer Suffix Like Adjacency")]
    public void LexShould_34([NotNull] string source, int boundary)
    {
        LexerResult result = Lex(source);

        result.Diagnostics
            .Should()
            .ContainSingle();

        SushiDiagnostic diagnostic = result.Diagnostics.Single();

        diagnostic.Severity
            .Should()
            .Be(DiagnosticSeverity.Error);

        diagnostic.Span.Start
            .Should()
            .Be(boundary);

        diagnostic.Span.End
            .Should()
            .Be(boundary);
    }

    [TestCase("42 foo", TestName = "Lex Should Allow Whitespace Between Separation Required Elements")]
    [TestCase("42\nfoo", TestName = "Lex Should Allow Line Terminator Between Separation Required Elements")]
    [TestCase("42/* comment */foo", TestName = "Lex Should Allow Comment Between Separation Required Elements")]
    [TestCase("42$foo", TestName = "Lex Should Allow Unknown Recovery Element Between Separation Required Elements")]
    [TestCase("42+foo", TestName = "Lex Should Allow Other Source Element Between Separation Required Elements")]
    public void LexShould_35([NotNull] string source)
    {
        LexerResult result = Lex(source);

        result.Diagnostics
            .Should()
            .NotContain(diagnostic => diagnostic.Span.Length == 0);
    }

    [TestCase(".", TestName = "Lex Should Recognize Dot Punctuation")]
    [TestCase(";", TestName = "Lex Should Recognize Semicolon Punctuation")]
    [TestCase("(", TestName = "Lex Should Recognize Open Parenthesis Punctuation")]
    [TestCase(")", TestName = "Lex Should Recognize Close Parenthesis Punctuation")]
    [TestCase("{", TestName = "Lex Should Recognize Open Brace Punctuation")]
    [TestCase("}", TestName = "Lex Should Recognize Close Brace Punctuation")]
    public void LexShould_36([NotNull] string source)
    {
        LexerResult result = Lex(source);

        AssertTokens(result, (LexTokenType.Punctuation, 0, 1));

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Recognize Minimum Function Declaration")]
    public void LexShould_37()
    {
        const string source = "public int32 main() { return 42; }";

        LexerResult result = Lex(source);

        AssertTokens(result,
            (LexTokenType.Keyword, 0, 6),
            (LexTokenType.Whitespace, 6, 7),
            (LexTokenType.Identifier, 7, 12),
            (LexTokenType.Whitespace, 12, 13),
            (LexTokenType.Identifier, 13, 17),
            (LexTokenType.Punctuation, 17, 18),
            (LexTokenType.Punctuation, 18, 19),
            (LexTokenType.Whitespace, 19, 20),
            (LexTokenType.Punctuation, 20, 21),
            (LexTokenType.Whitespace, 21, 22),
            (LexTokenType.Keyword, 22, 28),
            (LexTokenType.Whitespace, 28, 29),
            (LexTokenType.IntegerLiteral, 29, 31),
            (LexTokenType.Punctuation, 31, 32),
            (LexTokenType.Whitespace, 32, 33),
            (LexTokenType.Punctuation, 33, 34)
            );

        result.Diagnostics
            .Should()
            .BeEmpty();
    }

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        SourceLexer lexer = new();

        return lexer.Lex(snapshot, diagnosticReporter, CancellationToken.None);
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

    private static IEnumerable<TestCaseData> KeywordCases()
    {
        foreach (string keyword in keywords)
        {
            yield return new TestCaseData(keyword)
                .SetName($"Lex Should Recognize Reserved Keyword \"{keyword}\"");
        }
    }

    private static IEnumerable<TestCaseData> ClassifyBuiltInIntegerTypeCases()
    {
        foreach (string typeName in builtInIntegerTypeNames)
        {
            yield return new TestCaseData(typeName)
                .SetName($"Lex Should Classify Built-In Integer Type \"{typeName}\" As Identifier");
        }
    }

    private static IEnumerable<TestCaseData> EscapeBuiltInIntegerTypeCases()
    {
        foreach (string typeName in builtInIntegerTypeNames)
        {
            yield return new TestCaseData(typeName)
                .SetName($"Lex Should Recognize Escaped Built-In Integer Type \"{typeName}\"");
        }
    }

    private static readonly string[] keywords =
    [
        "not",
        "and",
        "or",
        "if",
        "else",
        "switch",
        "case",
        "default",
        "noop",
        "while",
        "do",
        "for",
        "foreach",
        "in",
        "break",
        "continue",
        "return",
        "propagate",
        "panic",
        "exit",
        "namespace",
        "package",
        "using",
        "alias",
        "except",
        "global",
        "typeof",
        "is",
        "exactly",
        "inherits",
        "implements",
        "extends",
        "leaf",
        "of",
        "public",
        "internal",
        "protected",
        "private",
        "static",
        "void"
    ];

    private static readonly string[] builtInIntegerTypeNames =
    [
        "int8",
        "uint8",
        "int16",
        "uint16",
        "int32",
        "uint32",
        "int64",
        "uint64",
        "int128",
        "uint128"
    ];
}