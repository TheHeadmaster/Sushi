using System.Diagnostics.CodeAnalysis;
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
public class ShiftExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Shifts.sus");

    [TestCase("1 << 2", "<<", TestName = "Parser Should Recognize Left Shift")]
    [TestCase("8 >> 1", ">>", TestName = "Parser Should Recognize Right Shift")]
    public void ParserShould_0(string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        shift.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OperatorToken);

        shift.OperatorToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.Operator);

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be(expectedOperator);

        shift.OperatorToken.Span.Length
            .Should()
            .Be(2);

        shift.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        shift.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        shift.Span.Start
            .Should()
            .Be(0);

        shift.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Addition More Tightly Than Shift Count")]
    public void ParserShould_1()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << 2 + 3");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = shift.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(addition.OperatorToken)
            .Should()
            .Be("+");

        shift.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Addition More Tightly Than Left Shift")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + 2 << 3");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = shift.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(addition.OperatorToken)
            .Should()
            .Be("+");

        shift.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Multiplication More Tightly Than Shift")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + 2 << 3 * 4");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = shift.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax multiplication = shift.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(addition.OperatorToken)
            .Should()
            .Be("+");

        GetSpelling(multiplication.OperatorToken)
            .Should()
            .Be("*");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("8 >> 1 >> 1", ">>", ">>", TestName = "Parser Should Associate Repeated Right Shift To The Left")]
    [TestCase("1 << 2 << 3", "<<", "<<", TestName = "Parser Should Associate Repeated Left Shift To The Left")]
    [TestCase("1 << 2 >> 3", ">>", "<<", TestName = "Parser Should Associate Mixed Shifts To The Left")]
    [TestCase("8 >> 1 << 2", "<<", ">>", TestName = "Parser Should Associate Reverse Mixed Shifts To The Left")]
    public void ParserShould_4(string source, string expectedOuterOperator, string expectedInnerOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax inner = outer.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(outer.OperatorToken)
            .Should()
            .Be(expectedOuterOperator);

        GetSpelling(inner.OperatorToken)
            .Should()
            .Be(expectedInnerOperator);

        inner.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        inner.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

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

    [TestCase(TestName = "Parser Should Preserve Explicitly Grouped Shift Operand")]
    public void ParserShould_5()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << (2 >> 1)");

        BinaryExpressionSyntax leftShift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = leftShift.Right
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax rightShift = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(leftShift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(rightShift.OperatorToken)
            .Should()
            .Be(">>");

        grouped.CloseParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Allow Parenthesized Shift Before Addition")]
    public void ParserShould_6()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("(1 << 2) + 3");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = addition.Left
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax shift = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(addition.OperatorToken)
            .Should()
            .Be("+");

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Unary Prefix Operators More Tightly Than Shifts")]
    public void ParserShould_7()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("-1 << ~2");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = shift.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax complement = shift.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(negation.OperatorToken)
            .Should()
            .Be("-");

        GetSpelling(complement.OperatorToken)
            .Should()
            .Be("~");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 << 2 < 3", "<", TestName = "Parser Should Bind Shift More Tightly Than Relational Comparison")]
    [TestCase("1 << 2 == 4", "==", TestName = "Parser Should Bind Shift More Tightly Than Equality")]
    public void ParserShould_8(string source, string expectedComparison)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax shift = comparison.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(comparison.OperatorToken)
            .Should()
            .Be(expectedComparison);

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Preserve Comparison Nonassociativity Around Shifts")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << 2 < 3 < 4");

        InvalidComparisonChainSyntax chain = expression
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        BinaryExpressionSyntax comparison = chain.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax shift = comparison.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(chain.OperatorToken)
            .Should()
            .Be("<");

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Bind Shifts More Tightly Than Logical Expressions")]
    public void ParserShould_10()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << 2 == 4 and true");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax equality = conjunction.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax shift = equality.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        GetSpelling(equality.OperatorToken)
            .Should()
            .Be("==");

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        conjunction.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 << ;", "<<", TestName = "Parser Should Preserve Semicolon After Missing Left Shift Operand")]
    [TestCase("1 >> ;", ">>", TestName = "Parser Should Preserve Semicolon After Missing Right Shift Operand")]
    public void ParserShould_11([NotNull] string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = shift.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be(expectedOperator);

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
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Preserve Nested Shift Syntax With Missing Final Operand")]
    public void ParserShould_12()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << 2 >> ;");

        BinaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax inner = outer.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = outer.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        GetSpelling(outer.OperatorToken)
            .Should()
            .Be(">>");

        GetSpelling(inner.OperatorToken)
            .Should()
            .Be("<<");

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.TryConsumePunctuation((byte)';', out _)
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Synthesize Missing Shift Operand At EOF")]
    public void ParserShould_13()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 <<");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = shift.Right
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
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Preserve Unknown Recovery Element After Shift")]
    public void ParserShould_14()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << $ 2");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = shift.Right
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
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Ignore Comment Trivia Around Shift Operator")]
    public void ParserShould_15()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1/* before */<</* after */2");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Preserve UTF8 Byte Offsets For Shifts")]
    public void ParserShould_16()
    {
        LexerResult lexerResult = Lex("雪 1 << 2");
        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        ExpressionParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        cursor.TryAdvance(out LexToken unknown)
            .Should()
            .BeTrue();

        unknown.Type
            .Should()
            .Be(LexTokenType.Unknown);

        ExpressionSyntax expression = parser.ParseExpression(reporter);

        expression.Span.Start
            .Should()
            .Be("雪 "u8.Length);

        expression.Span.End
            .Should()
            .Be(lexerResult.Snapshot.SourceLength);

        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Executable MVP Should Bind Constant Shift Expression")]
    public void ParserShould_17()
    {
        const string source = "public int32 launch() { return 1 << 2; }";
    
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult result = SourceParser.ParseFunctionDeclaration(lexerResult, reporter, CancellationToken.None);
    
        FunctionDeclarationSyntax function = result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;
    
        ReturnStatementSyntax statement = function.Body.Statements[0]
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;
    
        statement.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>();
    
        FunctionBindResult bound = FunctionDeclarationBinder.Bind(function, reporter, CancellationToken.None);
    
        bound.Function
            .Should()
            .NotBeNull();
    
        BoundFunction boundFunction = bound.Function!;
    
        BoundReturnStatement boundReturn = boundFunction.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;
    
        BoundIntegerLiteralExpression constant = boundReturn.Expression
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;
    
        constant.Value
            .Should()
            .Be(4);
    
        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Honor Cancellation During Shift Parsing")]
    public void ParserShould_18()
    {
        using CancellationTokenSource cancellation = new();

        ParserCursor cursor = new(Lex("1 << 2"), cancellation.Token);
        ExpressionParser parser = new(cursor, cancellation.Token);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        cancellation.Cancel();

        Action action = () => parser.ParseExpression(reporter);

        action
            .Should()
            .Throw<OperationCanceledException>();

        cursor.Index
            .Should()
            .Be(0);
    }

    private static string GetSpelling(SyntaxToken token)
    {
        SourceSpan span = token.Span;
        return System.Text.Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
    }

    private static (ExpressionSyntax Expression, ParserCursor Cursor, IReadOnlyList<SushiDiagnostic> Diagnostics) ParseExpression(string source)
    {
        LexerResult lexerResult = Lex(source);
        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        ExpressionParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        ExpressionSyntax expression = parser.ParseExpression(reporter);

        return (expression, cursor, reporter.ReportDiagnostics());
    }

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult result = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);

        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return result;
    }
}