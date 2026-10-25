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
public class ComparisonExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Comparisons.sus");

    [TestCase("1 == 2", "==", TestName = "Parser Should Recognize Equality")]
    [TestCase("1 != 2", "!=", TestName = "Parser Should Recognize Inequality")]
    [TestCase("1 < 2", "<", TestName = "Parser Should Recognize Less Than")]
    [TestCase("1 <= 2", "<=", TestName = "Parser Should Recognize Less Than Or Equal")]
    [TestCase("1 > 2", ">", TestName = "Parser Should Recognize Greater Than")]
    [TestCase("1 >= 2", ">=", TestName = "Parser Should Recognize Greater Than Or Equal")]
    public void ParserShould_0(string source, string expectedOperator)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        comparison.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OperatorToken);

        GetSpelling(comparison.OperatorToken)
            .Should()
            .Be(expectedOperator);

        comparison.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        comparison.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        comparison.Span.Start
            .Should()
            .Be(0);

        comparison.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Relational Comparisons More Tightly Than Equality")]
    public void ParserShould_1()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 < 2 == 3 > 4");

        BinaryExpressionSyntax equality = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax lessThan = equality.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax greaterThan = equality.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(equality.OperatorToken)
            .Should()
            .Be("==");

        GetSpelling(lessThan.OperatorToken)
            .Should()
            .Be("<");

        GetSpelling(greaterThan.OperatorToken)
            .Should()
            .Be(">");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Accept Relational Comparisons On Both Sides Of Equality")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 < 2 == 3 < 4");

        BinaryExpressionSyntax equality = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        equality.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        equality.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        GetSpelling(equality.OperatorToken)
            .Should()
            .Be("==");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
            
        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Equality More Tightly Than Logical And And Or")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true or 1 == 2 and false");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax conjunction = disjunction.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax equality = conjunction.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        disjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OrKeyword);

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        GetSpelling(equality.OperatorToken)
            .Should()
            .Be("==");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Not More Tightly Than Equality")]
    public void ParserShould_4()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not true == false");

        BinaryExpressionSyntax equality = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = equality.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        equality.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        negation.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 < 2 < 3", "<", 6, TestName = "Parser Should Reject Relational Chain")]
    [TestCase("1 <= 2 > 3", ">", 7, TestName = "Parser Should Reject Mixed Relational Chain")]
    [TestCase("1 == 2 != 3", "!=", 7, TestName = "Parser Should Reject Mixed Equality Chain")]
    public void ParserShould_5(string source, [NotNull] string expectedOperator, int diagnosticStart)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        InvalidComparisonChainSyntax chain = expression
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        chain.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        chain.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        GetSpelling(chain.OperatorToken)
            .Should()
            .Be(expectedOperator);

        SushiDiagnostic diagnostic = diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison))
            .Which;

        diagnostic.Span.Start
            .Should()
            .Be(diagnosticStart);

        diagnostic.Span.Length
            .Should()
            .Be(expectedOperator.Length);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Diagnose Each Additional Chained Relational Operator")]
    public void ParserShould_6()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 < 2 < 3 < 4");

        InvalidComparisonChainSyntax outer = expression
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        InvalidComparisonChainSyntax inner = outer.Left
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        inner.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        outer.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        diagnostics
            .Where(item => item.IsType(ErrorType.NonAssociativeComparison))
            .Select(item => item.Span.Start)
            .Should()
            .Equal(6, 10);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase("(1 < 2) < 3", TestName = "Parser Should Permit Parenthesized Relational Left Operand")]
    [TestCase("1 < (2 < 3)", TestName = "Parser Should Permit Parenthesized Relational Right Operand")]
    [TestCase("(1 == 2) == true", TestName = "Parser Should Permit Parenthesized Equality")]
    public void ParserShould_7(string source)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        bool hasGroupedOperand = outer.Left is ParenthesizedExpressionSyntax || outer.Right is ParenthesizedExpressionSyntax;

        hasGroupedOperand
            .Should()
            .BeTrue();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Allow Higher Precedence Relational Comparison Inside Equality Right Operand")]
    public void ParserShould_8()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 == 2 < 3");

        BinaryExpressionSyntax equality = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax relational = equality.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(equality.OperatorToken)
            .Should()
            .Be("==");

        GetSpelling(relational.OperatorToken)
            .Should()
            .Be("<");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Bind Arithmetic More Tightly Than Relational Comparison")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + 2 < 3 * 4");

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = comparison.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax multiplication = comparison.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(comparison.OperatorToken)
            .Should()
            .Be("<");

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

    [TestCase(TestName = "Parser Should Retain Function Body After Invalid Comparison Chain")]
    public void ParserShould_10()
    {
        const string source = "public int32 launch() { return 1 < 2 < 3; }";

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
            .BeOfType<InvalidComparisonChainSyntax>();

        statement.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        function.Body.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));
    }

    [TestCase(TestName = "Parser Should Preserve Semicolon After Missing Comparison Operand")]
    public void ParserShould_11()
    {
        const string source = "1 <= ;";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;
        
        IntegerLiteralExpressionSyntax missing = comparison.Right
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
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Bind Shift More Tightly Than Relational Comparison")]
    public void ParserShould_12()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 << 2 < 3");

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
            .Be("<");

        GetSpelling(shift.OperatorToken)
            .Should()
            .Be("<<");

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

    [TestCase(TestName = "Parser Should Preserve Unknown After Comparison Operator")]
    public void ParserShould_13()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 != $ 2");

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = comparison.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        cursor.TryPeek(out LexToken token)
            .Should()
            .BeTrue();

        token.Type
            .Should()
            .Be(LexTokenType.Unknown);

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Parser Should Preserve UTF8 Byte Offsets For Comparisons")]
    public void ParserShould_14()
    {
        LexerResult lexerResult = Lex("雪 1 <= 2");
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

    [TestCase(TestName = "Parser Should Ignore Comments Around Comparison Operators")]
    public void ParserShould_15()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1/* before */<=/* after */2");

        BinaryExpressionSyntax comparison = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        GetSpelling(comparison.OperatorToken)
            .Should()
            .Be("<=");

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Parser Should Reject Equality Chain Containing Relational Operands")]
    public void ParserShould_16()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 < 2 == 3 < 4 == 5");

        InvalidComparisonChainSyntax chain = expression
            .Should()
            .BeOfType<InvalidComparisonChainSyntax>()
            .Subject;

        chain.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        GetSpelling(chain.OperatorToken)
            .Should()
            .Be("==");

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Executable MVP Should Reject Boolean Comparison Result In Int32 Function")]
    public void ParserShould_17()
    {
        const string source = "public int32 launch() { return 1 == 2; }";

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult result = SourceParser.ParseFunctionDeclaration(lexerResult, reporter, CancellationToken.None);

        FunctionDeclarationSyntax function = result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

        FunctionBindResult bound = FunctionDeclarationBinder.Bind(function, reporter, CancellationToken.None);

        bound.Function.Should().BeNull();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Parser Should Honor Cancellation")]
    public void ParserShould_18()
    {
        using CancellationTokenSource cancellation = new();

        ParserCursor cursor = new(Lex("1 < 2"), cancellation.Token);
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

    [TestCase(TestName = "Executable MVP Should Bind Integer Comparison As Boolean Result")]
    public void ParserShould_19()
    {
        const string source = "public bool launch() { return 1 + 2 < 3 * 4; }";
    
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult result = SourceParser.ParseFunctionDeclaration(lexerResult, reporter, CancellationToken.None);
    
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
            .BeOfType<BinaryExpressionSyntax>();
    
        FunctionBindResult bound = FunctionDeclarationBinder.Bind(declaration, reporter, CancellationToken.None);
    
        bound.Function
            .Should()
            .NotBeNull();
    
        BoundFunction function = bound.Function!;
    
        function.ReturnType
            .Should()
            .Be(BoundType.Bool);
    
        BoundReturnStatement boundReturn = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;
    
        BoundBooleanConstantExpression constant = boundReturn.Expression
            .Should()
            .BeOfType<BoundBooleanConstantExpression>()
            .Subject;
    
        constant.Value
            .Should()
            .BeTrue();
    
        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
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