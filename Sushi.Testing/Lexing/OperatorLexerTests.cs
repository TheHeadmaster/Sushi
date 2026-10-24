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

    private static (LexerResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics) Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult result = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);

        return (result, diagnosticReporter.ReportDiagnostics());
    }
}