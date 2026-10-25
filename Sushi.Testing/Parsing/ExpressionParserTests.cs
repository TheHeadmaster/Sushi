using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class ExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Expressions.sus");

    [TestCase("42", TestName = "Expression Parser Should Parse Decimal Integer Literal")]
    [TestCase("b#1010", TestName = "Expression Parser Should Parse Binary Integer Literal")]
    [TestCase("o#755", TestName = "Expression Parser Should Parse Octal Integer Literal")]
    [TestCase("x#dead_beef", TestName = "Expression Parser Should Parse Hexadecimal Integer Literal")]
    public void ParserShould_0(string source)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        diagnostics
            .Should()
            .BeEmpty();

        IntegerLiteralExpressionSyntax literal = expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        literal.LiteralToken.Type
            .Should()
            .Be(SyntaxType.IntegerLiteralToken);

        literal.LiteralToken.IsSourceBacked
            .Should()
            .BeTrue();

        literal.Span.Start
            .Should()
            .Be(0);

        literal.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Trivia And Leave Following Semicolon Unconsumed")]
    public void ParserShould_1()
    {
        const string source = "  /* before */ 42 /* after */ ;";
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        diagnostics
            .Should()
            .BeEmpty();

        expression.Span.Start
            .Should()
            .Be(source.IndexOf("42", StringComparison.Ordinal));

        expression.Span.End
            .Should()
            .Be(source.IndexOf("42", StringComparison.Ordinal) + 2);

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(LexTokenType.Punctuation);

        cursor.Position
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));
    }

    [TestCase("", TestName = "Expression Parser Should Synthesize Missing Literal At Empty Source EOF")]
    [TestCase("  /* trivia */ ", TestName = "Expression Parser Should Synthesize Missing Literal After Trailing Trivia")]
    public void ParserShould_2(string source)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        IntegerLiteralExpressionSyntax literal = expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        literal.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        literal.Span.Start
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        literal.Span.Length
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        cursor.Index
            .Should()
            .Be(0);
    }

    [TestCase("Name;", LexTokenType.Identifier, TestName = "Expression Parser Should Preserve Unexpected Identifier")]
    [TestCase("return 42;", LexTokenType.Keyword, TestName = "Expression Parser Should Preserve Unexpected Keyword")]
    [TestCase(";42", LexTokenType.Punctuation, TestName = "Expression Parser Should Preserve Unexpected Punctuation")]
    [TestCase("$ 42", LexTokenType.Unknown, TestName = "Expression Parser Should Not Skip Unknown Element During Expression Recovery")]
    public void ParserShould_3(string source, LexTokenType expectedType)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        IntegerLiteralExpressionSyntax literal = expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        literal.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        literal.Span.Length
            .Should()
            .Be(0);

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(expectedType);

        literal.Span.Start
            .Should()
            .Be(next.Span.Start);

        cursor.Index
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Use The Cursor Position After A Previous Grammar Token")]
    public void ParserShould_4()
    {
        LexerResult lexerResult = Lex("return 42;");
        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        ExpressionParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        cursor.TryConsumeKeyword("return"u8, out _)
            .Should()
            .BeTrue();

        ExpressionSyntax expression = parser.ParseExpression(diagnosticReporter);

        expression.Span.Start
            .Should()
            .Be("return ".Length);

        cursor.TryConsumePunctuation((byte)';', out LexToken semicolon)
            .Should()
            .BeTrue();

        semicolon.Span.End
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Use Canonical UTF8 Byte Offsets")]
    public void ParserShould_5()
    {
        LexerResult lexerResult = Lex("雪 42");
        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        ExpressionParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        cursor.TryAdvance(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);

        ExpressionSyntax expression = parser.ParseExpression(diagnosticReporter);

        expression.Span.Start
            .Should()
            .Be("雪 "u8.Length);

        expression.Span.End
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Honor Cancellation Before Parsing")]
    public void ParserShould_6()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        ParserCursor cursor = new(Lex("42"), cancellationTokenSource.Token);
        ExpressionParser parser = new(cursor, cancellationTokenSource.Token);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        cancellationTokenSource.Cancel();

        Action action = () => parser.ParseExpression(diagnosticReporter);

        action
            .Should()
            .Throw<OperationCanceledException>();

        cursor.Index
            .Should()
            .Be(0);
    }

    private static (ExpressionSyntax Expression, ParserCursor Cursor, IReadOnlyList<SushiDiagnostic> Diagnostics) ParseExpression(string source)
    {
        LexerResult lexerResult = Lex(source);
        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        ExpressionParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        ExpressionSyntax expression = parser.ParseExpression(diagnosticReporter);

        return (expression, cursor, diagnosticReporter.ReportDiagnostics());
    }

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return lexerResult;
    }
}
