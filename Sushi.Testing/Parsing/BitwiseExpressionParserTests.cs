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
public class BitwiseExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Bitwise.sus");

    [TestCase("1 & 2", "&", TestName = "Parser Should Recognize Bitwise And")]
    [TestCase("1 ^ 2", "^", TestName = "Parser Should Recognize Bitwise Xor")]
    [TestCase("1 | 2", "|", TestName = "Parser Should Recognize Bitwise Or")]
    public void ParserShould_0(string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax binary = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        binary.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OperatorToken);

        binary.OperatorToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.Operator);

        GetSpelling(binary.OperatorToken)
            .Should()
            .Be(expectedOperator);

        binary.OperatorToken.Span.Length
            .Should()
            .Be(1);

        binary.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        binary.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        binary.Span.Start
            .Should()
            .Be(0);

        binary.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Bitwise And More Tightly Than Xor And Or")]
    public void ParserShould_1()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 | 2 ^ 3 & 4");

        BinaryExpressionSyntax bitwiseOr = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax xor = bitwiseOr.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwiseAnd = xor.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(bitwiseOr.OperatorToken)
            .Should()
            .Be("|");

        GetSpelling(xor.OperatorToken)
            .Should()
            .Be("^");

        GetSpelling(bitwiseAnd.OperatorToken)
            .Should()
            .Be("&");

        bitwiseOr.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        xor.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        bitwiseAnd.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        bitwiseAnd.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 << 2 & 3", true, TestName = "Parser Should Bind Left Shift More Tightly Than Bitwise And")]
    [TestCase("1 & 2 << 3", false, TestName = "Parser Should Bind Shift In Right Operand More Tightly Than Bitwise And")]
    public void ParserShould_2(string source, bool shiftOnLeft)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax bitwiseAnd = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax shift = (shiftOnLeft ? bitwiseAnd.Left : bitwiseAnd.Right)
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(bitwiseAnd.OperatorToken)
            .Should()
            .Be("&");

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

    [TestCase("1 + 2 ^ 3", true, "+", TestName = "Parser Should Bind Addition More Tightly Than Xor")]
    [TestCase("1 ^ 2 * 3", false, "*", TestName = "Parser Should Bind Multiplication More Tightly Than Xor")]
    public void ParserShould_3(string source, bool arithmeticOnLeft, string expectedArithmeticOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax xor = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax arithmetic = (arithmeticOnLeft ? xor.Left : xor.Right)
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(xor.OperatorToken)
            .Should()
            .Be("^");

        GetSpelling(arithmetic.OperatorToken)
            .Should()
            .Be(expectedArithmeticOperator);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 & 2 < 3", "&", "<", TestName = "Parser Should Bind Bitwise And More Tightly Than Relational")]
    [TestCase("1 | 2 == 3", "|", "==", TestName = "Parser Should Bind Bitwise Or More Tightly Than Equality")]
    public void ParserShould_4(string source, string expectedBitwiseOperator, string expectedComparisonOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwise = comparison.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(comparison.OperatorToken)
            .Should()
            .Be(expectedComparisonOperator);

        GetSpelling(bitwise.OperatorToken)
            .Should()
            .Be(expectedBitwiseOperator);

        comparison.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 & 2 & 3", "&", TestName = "Parser Should Associate Bitwise And To The Left")]
    [TestCase("1 ^ 2 ^ 3", "^", TestName = "Parser Should Associate Bitwise Xor To The Left")]
    [TestCase("1 | 2 | 3", "|", TestName = "Parser Should Associate Bitwise Or To The Left")]
    public void ParserShould_5(string source, string expectedOperator)
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
            .Be(expectedOperator);

        GetSpelling(inner.OperatorToken)
            .Should()
            .Be(expectedOperator);

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

    [TestCase(TestName = "Parser Should Preserve Explicit Grouping Over Bitwise Precedence")]
    public void ParserShould_6()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 & (2 | 3)");

        BinaryExpressionSyntax bitwiseAnd = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = bitwiseAnd.Right
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwiseOr = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(bitwiseAnd.OperatorToken)
            .Should()
            .Be("&");

        GetSpelling(bitwiseOr.OperatorToken)
            .Should()
            .Be("|");

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

    [TestCase(TestName = "Parser Should Preserve Grouped Bitwise Expression Before Higher Precedence Shift")]
    public void ParserShould_7()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("(1 | 2) << 3");

        BinaryExpressionSyntax shift = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = shift.Left
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwiseOr = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

        GetSpelling(bitwiseOr.OperatorToken)
            .Should()
            .Be("|");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Unary Complement More Tightly Than Bitwise Xor")]
    public void ParserShould_8()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("~1 ^ 2");

        BinaryExpressionSyntax xor = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax complement = xor.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        GetSpelling(xor.OperatorToken)
            .Should()
            .Be("^");

        GetSpelling(complement.OperatorToken)
            .Should()
            .Be("~");

        complement.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true & false", "&", TestName = "Parser Should Parse Eager Boolean And")]
    [TestCase("true ^ false", "^", TestName = "Parser Should Parse Eager Boolean Xor")]
    [TestCase("true | false", "|", TestName = "Parser Should Parse Eager Boolean Or")]
    public void ParserShould_9(string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax binary = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(binary.OperatorToken)
            .Should()
            .Be(expectedOperator);

        binary.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OperatorToken);

        binary.Left
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        binary.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true & false and true", SyntaxType.AndKeyword, "&", TestName = "Parser Should Distinguish Eager And From Logical And")]
    [TestCase("true | false or true", SyntaxType.OrKeyword, "|", TestName = "Parser Should Distinguish Eager Or From Logical Or")]
    public void ParserShould_10(string source, SyntaxType expectedLogicalType, string expectedBitwiseOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax logical = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwise = logical.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        logical.OperatorToken.Type
            .Should()
            .Be(expectedLogicalType);

        GetSpelling(bitwise.OperatorToken)
            .Should()
            .Be(expectedBitwiseOperator);

        logical.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Preserve Comparison Nonassociativity Around Bitwise Operators")]
    public void ParserShould_11()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 & 2 < 3 < 4");

        InvalidComparisonChainSyntax chain = expression
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        BinaryExpressionSyntax comparison = chain.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwiseAnd = comparison.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(chain.OperatorToken)
            .Should()
            .Be("<");

        GetSpelling(bitwiseAnd.OperatorToken)
            .Should()
            .Be("&");

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase("1 & ;", "&", TestName = "Parser Should Preserve Semicolon After Missing Bitwise And Operand")]
    [TestCase("1 | ;", "|", TestName = "Parser Should Preserve Semicolon After Missing Bitwise Or Operand")]
    public void ParserShould_12([NotNull] string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax binary = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = binary.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        GetSpelling(binary.OperatorToken)
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

    [TestCase(TestName = "Parser Should Preserve Nested Bitwise Syntax With Missing Final Operand")]
    public void ParserShould_13()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 | 2 & ;");

        BinaryExpressionSyntax bitwiseOr = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax bitwiseAnd = bitwiseOr.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = bitwiseAnd.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        GetSpelling(bitwiseOr.OperatorToken)
            .Should()
            .Be("|");

        GetSpelling(bitwiseAnd.OperatorToken)
            .Should()
            .Be("&");

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

    [TestCase(TestName = "Parser Should Synthesize Missing Bitwise Operand At EOF")]
    public void ParserShould_14()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 |");

        BinaryExpressionSyntax bitwiseOr = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = bitwiseOr.Right
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

    [TestCase(TestName = "Parser Should Preserve Unknown Element After Bitwise Operator")]
    public void ParserShould_15()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 & $ 2");

        BinaryExpressionSyntax bitwiseAnd = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = bitwiseAnd.Right
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

    [TestCase(TestName = "Parser Should Ignore Comment Trivia Around Bitwise Xor")]
    public void ParserShould_16()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1/* before */^/* after */2");

        BinaryExpressionSyntax xor = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(xor.OperatorToken)
            .Should()
            .Be("^");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Preserve UTF8 Byte Offsets For Bitwise Expressions")]
    public void ParserShould_17()
    {
        LexerResult lexerResult = Lex("雪 1 | 2");
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

    [TestCase("1 & 2", TestName = "Executable MVP Should Diagnose Unsupported Integer Bitwise Expression")]
    [TestCase("true | false", TestName = "Executable MVP Should Diagnose Unsupported Boolean Bitwise Expression")]
    public void ParserShould_18(string expression)
    {
        string source = $"public int32 launch() {{ return {expression}; }}";

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
            .BeNull();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Parser Should Honor Cancellation During Bitwise Parsing")]
    public void ParserShould_19()
    {
        using CancellationTokenSource cancellation = new();

        ParserCursor cursor = new(Lex("1 & 2"), cancellation.Token);
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