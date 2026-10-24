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
public class UnaryPrefixExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Unary.sus");

    [TestCase("+42", "+", TestName = "Expression Parser Should Parse Unary Plus")]
    [TestCase("-42", "-", TestName = "Expression Parser Should Parse Unary Minus")]
    [TestCase("~42", "~", TestName = "Expression Parser Should Parse Bitwise Complement")]
    public void ParserShould_0(string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(unary)
            .Should()
            .Be(expectedOperator);

        unary.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OperatorToken);

        unary.OperatorToken.IsSourceBacked
            .Should()
            .BeTrue();

        unary.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        unary.Span.Start
            .Should()
            .Be(0);

        unary.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Bind Unary Minus More Tightly Than Multiplication")]
    public void ParserShould_1()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("-1 * 2");

        BinaryExpressionSyntax multiplication = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax unary = multiplication.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(unary)
            .Should()
            .Be("-");

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

    [TestCase(TestName = "Expression Parser Should Parse Unary Minus On Right Side Of Multiplication")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 * -2");

        BinaryExpressionSyntax multiplication = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax unary = multiplication.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(unary)
            .Should()
            .Be("-");

        multiplication.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Bind Complement More Tightly Than Addition")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("~1 + 2");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax complement = addition.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(complement)
            .Should()
            .Be("~");

        addition.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Nest Chained Prefix Unary Operators")]
    public void ParserShould_4()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("-~+42");

        UnaryExpressionSyntax minus = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax complement = minus.Operand
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax plus = complement.Operand
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(minus)
            .Should()
            .Be("-");

        GetOperator(complement)
            .Should()
            .Be("~");

        GetOperator(plus)
            .Should()
            .Be("+");

        plus.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Unary Operator Around Parenthesized Expression")]
    public void ParserShould_5()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("-(1 + 2) * 3");

        BinaryExpressionSyntax multiplication = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax minus = multiplication.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = minus.Operand
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        GetOperator(minus)
            .Should()
            .Be("-");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Distinguish Binary And Prefix Plus")]
    public void ParserShould_6()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + +2");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax plus = addition.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(plus)
            .Should()
            .Be("+");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Allow Separated Consecutive Minus Operators")]
    public void ParserShould_7()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("- -42");

        UnaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax inner = outer.Operand
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetOperator(outer)
            .Should()
            .Be("-");

        GetOperator(inner)
            .Should()
            .Be("-");

        inner.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Semicolon After Missing Unary Operand")]
    public void ParserShould_8()
    {
        const string source = "- ;";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = unary.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        missing.Span.Start
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));

        cursor.TryConsumePunctuation((byte)';', out _)
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Preserve Incomplete Unary Operand Inside Binary Expression")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + ~;");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax complement = addition.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = complement.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.TryConsumePunctuation((byte)';', out _)
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Preserve Unknown Element After Unary Operator")]
    public void ParserShould_10()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("- $ 42");

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = unary.Operand
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

    [TestCase(TestName = "Expression Parser Should Not Split Increment Or Decrement Into Unary Operators")]
    public void ParserShould_11()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("--42");

        IntegerLiteralExpressionSyntax missing = expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(LexTokenType.Operator);

        next.Span.Length
            .Should()
            .Be(2);

        cursor.Index
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Synthesize Unary Operand At EOF")]
    public void ParserShould_12()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("- ");

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = unary.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        missing.Span.Start
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        missing.Span.Length
            .Should()
            .Be(0);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Preserve UTF8 Byte Offsets For Unary Expressions")]
    public void ParserShould_13()
    {
        LexerResult lexerResult = Lex("雪 -42");
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

    [TestCase(TestName = "Executable MVP Should Diagnose Unsupported Prefix Unary Expression")]
    public void ParserShould_14()
    {
        const string source = "public int32 main() { return -42; }";

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);
        ParserResult result = SourceParser.ParseFunctionDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        FunctionDeclarationSyntax declaration = result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

        ReturnStatementSyntax statement = declaration.Body.Statements[0]
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        statement.Expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>();

        FunctionBindResult bound = FunctionDeclarationBinder.Bind(declaration, diagnosticReporter, CancellationToken.None);

        bound.Function
            .Should()
            .BeNull();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    private static string GetOperator(UnaryExpressionSyntax expression)
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

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return lexerResult;
    }
}