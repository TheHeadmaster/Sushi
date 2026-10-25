using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Testing.Lexing;

[TestFixture]
public class OperatorLexerTests
{
    private static readonly Uri testUri = new("file:///TestProject/Operators.sus");

    [TestCase("1+2", "+", TestName = "Lex Should Recognize Addition Operator")]
    [TestCase("1-2", "-", TestName = "Lex Should Recognize Subtraction Operator")]
    [TestCase("1*2", "*", TestName = "Lex Should Recognize Multiplication Operator")]
    [TestCase("1/2", "/", TestName = "Lex Should Recognize Division Operator")]
    [TestCase("1%2", "%", TestName = "Lex Should Recognize Remainder Operator")]
    [TestCase("1++2", "++", TestName = "Lex Should Keep Increment Operator Indivisible")]
    [TestCase("1--2", "--", TestName = "Lex Should Keep Decrement Operator Indivisible")]
    [TestCase("1~2", "~", TestName = "Lex Should Recognize Bitwise Complement Operator")]
    [TestCase("1==2", "==", TestName = "Lex Should Recognize Equality")]
    [TestCase("1!=2", "!=", TestName = "Lex Should Recognize Inequality")]
    [TestCase("1<2", "<", TestName = "Lex Should Recognize Less Than")]
    [TestCase("1<=2", "<=", TestName = "Lex Should Recognize Less Than Or Equal")]
    [TestCase("1>2", ">", TestName = "Lex Should Recognize Greater Than")]
    [TestCase("1>=2", ">=", TestName = "Lex Should Recognize Greater Than Or Equal")]
    [TestCase("1<<2", "<<", TestName = "Lex Should Preserve Left Shift As One Token")]
    [TestCase("1>>2", ">>", TestName = "Lex Should Preserve Right Shift As One Token")]
    [TestCase("1=2", "=", TestName = "Lex Should Recognize Assignment Symbol")]
    public void LexShould_0(string source, [NotNull] string expectedOperator)
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex(source);

        LexToken operatorToken = result.Tokens
            .Should()
            .ContainSingle(token => token.Type == LexTokenType.Operator)
            .Which;

        operatorToken.Span.Start
            .Should()
            .Be(1);

        operatorToken.Span.Length
            .Should()
            .Be(expectedOperator.Length);

        result.Snapshot.Bytes.Span[operatorToken.Span.Start..operatorToken.Span.End]
            .SequenceEqual(System.Text.Encoding.UTF8.GetBytes(expectedOperator))
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Preserve Comments When Division Becomes An Operator")]
    public void LexShould_1()
    {
        const string source = "8/* comment */2 // trailing";

        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex(source);

        result.Tokens
            .Count(token => token.Type == LexTokenType.BlockComment)
            .Should()
            .Be(1);

        result.Tokens
            .Count(token => token.Type == LexTokenType.LineComment)
            .Should()
            .Be(1);

        result.Tokens
            .Should()
            .NotContain(token => token.Type == LexTokenType.Operator);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Recognize Consecutive Complement Operators Independently")]
    public void LexShould_2()
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex("~~42");
    
        result.Tokens
            .Select(token => token.Type)
            .Should()
            .Equal(
                LexTokenType.Operator,
                LexTokenType.Operator,
                LexTokenType.IntegerLiteral
            );
    
        result.Tokens[0].Span.Length
            .Should()
            .Be(1);
    
        result.Tokens[1].Span.Start
            .Should()
            .Be(1);
    
        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Not Recognize Standalone Exclamation As Operator")]
    public void LexShould_3()
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex("1!2");

        result.Tokens
            .Select(token => token.Type)
            .Should()
            .Equal(
                LexTokenType.IntegerLiteral,
                LexTokenType.Unknown,
                LexTokenType.IntegerLiteral
            );

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Lex Should Prefer Equality Operator Before Following Assignment Symbol")]
    public void LexShould_4()
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex("1===2");

        result.Tokens
            .Select(token => token.Type)
            .Should()
            .Equal(
                LexTokenType.IntegerLiteral,
                LexTokenType.Operator,
                LexTokenType.Operator,
                LexTokenType.IntegerLiteral
            );

        result.Tokens[1].Span.Length
            .Should()
            .Be(2);

        result.Tokens[2].Span.Length
            .Should()
            .Be(1);

        diagnostics
            .Should()
            .BeEmpty();
    }

    private static (LexerResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics) Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult result = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }
}