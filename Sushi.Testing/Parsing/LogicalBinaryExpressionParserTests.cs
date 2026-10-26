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
public class LogicalBinaryExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Logical.sus");

    [TestCase(TestName = "Expression Parser Should Bind And More Tightly Than Or")]
    public void ParserShould_0()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true or false and not true");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax conjunction = disjunction.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = conjunction.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        disjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OrKeyword);

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        negation.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        disjunction.Left
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        conjunction.Left
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        negation.Operand
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true and false and true", SyntaxType.AndKeyword, TestName = "Expression Parser Should Associate And To The Left")]
    [TestCase("true or false or true", SyntaxType.OrKeyword, TestName = "Expression Parser Should Associate Or To The Left")]
    public void ParserShould_1(string source, SyntaxType expectedType)
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

        outer.OperatorToken.Type
            .Should()
            .Be(expectedType);

        inner.OperatorToken.Type
            .Should()
            .Be(expectedType);

        inner.Left
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        inner.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        outer.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Group And Before Following Or")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true and false or true");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax conjunction = disjunction.Left
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        disjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OrKeyword);

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Bind Arithmetic More Tightly Than Logical Operators")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true or false and 1 + 2 * 3");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax conjunction = disjunction.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax addition = conjunction.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax multiplication = addition.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        disjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OrKeyword);

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

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

    [TestCase(TestName = "Expression Parser Should Bind Not More Tightly Than And")]
    public void ParserShould_4()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not true and false");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = conjunction.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        negation.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        negation.Operand
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Explicit Grouping Across Logical Precedence")]
    public void ParserShould_5()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("(true or false) and true");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = conjunction.Left
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax disjunction = grouped.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);
            
        disjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.OrKeyword);

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

    [TestCase(TestName = "Expression Parser Should Preserve Comment Trivia Around Logical Operator")]
    public void ParserShould_6()
    {
        const string source = "true/* before */and/* after */false";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        GetSpelling(conjunction.OperatorToken)
            .Should()
            .Be("and");

        conjunction.Span.Start
            .Should()
            .Be(0);

        conjunction.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true and ;", SyntaxType.AndKeyword, TestName = "Expression Parser Should Preserve Semicolon After Missing And Operand")]
    [TestCase("true or ;", SyntaxType.OrKeyword, TestName = "Expression Parser Should Preserve Semicolon After Missing Or Operand")]
    public void ParserShould_7([NotNull] string source, SyntaxType expectedType)
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

        binary.OperatorToken.Type
            .Should()
            .Be(expectedType);

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

    [TestCase(TestName = "Expression Parser Should Preserve Unexpected Logical Operator During Recovery")]
    public void ParserShould_8()
    {
        const string source = "true and or false";

        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = conjunction.Right
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        cursor.TryPeek(out LexToken next)
            .Should()
            .BeTrue();

        next.Type
            .Should()
            .Be(LexTokenType.Keyword);

        cursor.TokenTextEquals(next, "or"u8)
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Expression Parser Should Propagate Incomplete Nested Logical Operand")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true or false and not ;");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        BinaryExpressionSyntax conjunction = disjunction.Right
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = conjunction.Right
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = negation.Operand
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

    [TestCase(TestName = "Expression Parser Should Synthesize Missing Logical Operand At EOF")]
    public void ParserShould_10()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true or");

        BinaryExpressionSyntax disjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;
        
        IntegerLiteralExpressionSyntax missing = disjunction.Right
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

    [TestCase(TestName = "Expression Parser Should Preserve Unknown Element After Logical Operator")]
    public void ParserShould_11()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true and $ false");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax missing = conjunction.Right
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

    [TestCase(TestName = "Expression Parser Should Parse Escaped Logical Keyword As Identifier Operand")]
    public void ParserShould_12()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("true and @or");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        IdentifierExpressionSyntax identifier = conjunction.Right
            .Should()
            .BeOfType<IdentifierExpressionSyntax>()
            .Subject;

        identifier.IdentifierToken.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);

        identifier.IdentifierToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.EscapedIdentifier);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Leave Logical Operand Typing To Semantic Analysis")]
    public void ParserShould_13()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("42 and true");

        BinaryExpressionSyntax conjunction = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        conjunction.OperatorToken.Type
            .Should()
            .Be(SyntaxType.AndKeyword);

        conjunction.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

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

    [TestCase(TestName = "Executable MVP Should Reject Logical Boolean Result In Int32 Function")]
    public void ParserShould_14()
    {
        const string source = "public int32 main() { return true and false; }";

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, diagnosticReporter, CancellationToken.None);
        ParserResult parserResult = SourceParser.ParseFunctionDeclaration(lexerResult, diagnosticReporter, CancellationToken.None);

        FunctionDeclarationSyntax declaration = parserResult.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

        ReturnStatementSyntax statement = declaration.Body.Statements[0]
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        statement.Expression.Should().BeOfType<BinaryExpressionSyntax>();

        FunctionBindResult bound = FunctionDeclarationBinder.Bind(declaration, diagnosticReporter, CancellationToken.None);

        bound.Function
            .Should()
            .BeNull();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Expression Parser Should Honor Cancellation")]
    public void ParserShould_15()
    {
        using CancellationTokenSource cancellationTokenSource = new();

        ParserCursor cursor = new(Lex("true and false"), cancellationTokenSource.Token);
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

    [TestCase(TestName = "Expression Parser Should Preserve UTF8 Byte Offsets")]
    public void ParserShould_16()
    {
        LexerResult lexerResult = Lex("雪 true or false");
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

        diagnosticReporter
            .ReportDiagnostics()
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