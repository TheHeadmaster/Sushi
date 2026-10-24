using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class PrattExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Pratt.sus");

    [TestCase(TestName = "Expression Parser Should Bind Multiplication More Tightly Than Addition")]
    public void ParserShould_0()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + 2 * 3");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax multiplication = addition.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetOperator(addition)
            .Should()
            .Be("+");

        GetOperator(multiplication)
            .Should()
            .Be("*");

        addition.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        multiplication.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        multiplication.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Explicit Parenthesized Precedence")]
    public void ParserShould_1()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("(1 + 2) * 3");

        BinaryExpressionSyntax multiplication = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = multiplication.Left
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetOperator(multiplication)
            .Should()
            .Be("*");

        GetOperator(addition)
            .Should()
            .Be("+");

        grouped.OpenParenthesisToken.IsMissing
            .Should()
            .BeFalse();
        
        grouped.CloseParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        grouped.Span.Start
            .Should()
            .Be(0);

        grouped.Span.End
            .Should()
            .Be("(1 + 2)".Length);

        cursor
            .TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Provisionally Associate Equal Tier Subtraction To The Left")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("10 - 4 - 2");

        BinaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax inner = outer.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetOperator(outer)
            .Should()
            .Be("-");

        GetOperator(inner)
            .Should()
            .Be("-");

        outer.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Parse Division And Remainder At Multiplicative Precedence")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("20 / 4 + 3 % 2");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax division = addition.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax remainder = addition.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetOperator(addition)
            .Should()
            .Be("+");

        GetOperator(division)
            .Should()
            .Be("/");

        GetOperator(remainder)
            .Should()
            .Be("%");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Semicolon After Missing Right Operand")]
    public void ParserShould_4()
    {
        const string source = "1 + ;";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax binary = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = binary.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        missing.Span.Start
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));

        missing.Span.Length
            .Should()
            .Be(0);

        cursor.TryConsumePunctuation((byte)';', out _)
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Stop Recovery At Unexpected Operator")]
    public void ParserShould_5()
    {
        const string source = "1 + * 2";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = addition.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.Position
            .Should()
            .Be(source.IndexOf('*', StringComparison.Ordinal));

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Not Skip Unknown Elements During Operand Recovery")]
    public void ParserShould_6()
    {
        const string source = "1 + $ 2";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = addition.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.TryPeek(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Recover Missing Closing Parenthesis")]
    public void ParserShould_7()
    {
        const string source = "(1 + 2";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        ParenthesizedExpressionSyntax grouped = expression
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        grouped.CloseParenthesisToken.IsMissing
            .Should()
            .BeTrue();

        grouped.CloseParenthesisToken.Span.Start
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        grouped.CloseParenthesisToken.Span.Length
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Preserve Closing Parenthesis After Missing Inner Expression")]
    public void ParserShould_8()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("()");

        ParenthesizedExpressionSyntax grouped = expression
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = grouped.Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        grouped.CloseParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        cursor
            .TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Not Interpret Increment As Two Addition Operators")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1++2");

        expression.Should().BeOfType<IntegerLiteralExpressionSyntax>();

        cursor
            .TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(LexTokenType.Operator);

        next.Span.Length
            .Should()
            .Be(2);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Ignore Comments Between Arithmetic Operands")]
    public void ParserShould_10()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1/* comment */+2");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetOperator(addition)
            .Should()
            .Be("+");

        cursor
            .TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Executable MVP Should Diagnose Newly Parsed Arithmetic Expression")]
    public void ParserShould_11()
    {
        const string source = "public int32 main() { return 1 + 2 * 3; }";

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);
        ParserResult parserResult = SourceParser.ParseFunctionDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        FunctionDeclarationSyntax declaration = parserResult.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

        FunctionBindResult bound = FunctionDeclarationBinder.Bind(declaration, diagnosticReporter, CancellationToken.None);

        bound.Function
            .Should()
            .BeNull();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Expression Parser Should Honor Cancellation")]
    public void ParserShould_12()
    {
        using CancellationTokenSource cancellationTokenSource = new();

        ParserCursor cursor = new(Lex("1 + 2"), cancellationTokenSource.Token);
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

    private static string GetOperator(BinaryExpressionSyntax expression)
    {
        SourceSpan span = expression.OperatorToken.Span;
        return System.Text.Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
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

        diagnosticReporter
            .ReportDiagnostics()
            .Should()
            .BeEmpty();

        return lexerResult;
    }
}