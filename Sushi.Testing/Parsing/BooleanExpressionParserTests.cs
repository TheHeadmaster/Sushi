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
public class BooleanExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Boolean.sus");

    [TestCase("true", TestName = "Expression Parser Should Parse True Literal")]
    [TestCase("false", TestName = "Expression Parser Should Parse False Literal")]
    public void ParserShould_0(string source)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        BooleanLiteralExpressionSyntax literal = expression
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>()
            .Subject;

        literal.Type
            .Should()
            .Be(SyntaxType.BooleanLiteralExpression);

        literal.LiteralToken.Type
            .Should()
            .Be(SyntaxType.BooleanLiteralToken);

        literal.LiteralToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.BooleanLiteral);

        literal.Span.Start
            .Should()
            .Be(0);

        literal.Span.End
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("not true", TestName = "Expression Parser Should Parse Not True")]
    [TestCase("not false", TestName = "Expression Parser Should Parse Not False")]
    public void ParserShould_1(string source)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        unary.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        unary.OperatorToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.Keyword);

        GetSpelling(unary.OperatorToken)
            .Should()
            .Be("not");

        unary.Operand
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

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

    [TestCase(TestName = "Expression Parser Should Nest Repeated Logical Negation")]
    public void ParserShould_2()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not not true");

        UnaryExpressionSyntax outer = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax inner = outer.Operand
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        outer.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        inner.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        inner.Operand
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Parentheses Under Logical Negation")]
    public void ParserShould_3()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not (false)");

        UnaryExpressionSyntax unary = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        ParenthesizedExpressionSyntax grouped = unary.Operand
            .Should()
            .BeOfType<ParenthesizedExpressionSyntax>()
            .Subject;

        grouped.Expression
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        grouped.OpenParenthesisToken.IsMissing
            .Should()
            .BeFalse();

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

    [TestCase(TestName = "Expression Parser Should Bind Not More Tightly Than Addition")]
    public void ParserShould_4()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not true + 1");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax negation = addition.Left
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        negation.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        negation.Operand
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

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

    [TestCase(TestName = "Expression Parser Should Share The Prefix Tier Between Not And Arithmetic Unary Operators")]
    public void ParserShould_5()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not -42");

        UnaryExpressionSyntax negation = expression
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        UnaryExpressionSyntax minus = negation.Operand
            .Should()
            .BeOfType<UnaryExpressionSyntax>()
            .Subject;

        negation.OperatorToken.Type
            .Should()
            .Be(SyntaxType.NotKeyword);

        GetSpelling(minus.OperatorToken)
            .Should()
            .Be("-");

        minus.Operand
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Parse Boolean Literal Inside Arithmetic Expression")]
    public void ParserShould_6()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("1 + true");

        BinaryExpressionSyntax addition = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        addition.Left
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        addition.Right
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Expression Parser Should Preserve Semicolon After Missing Not Operand")]
    public void ParserShould_7()
    {
        const string source = "not ;";

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

    [TestCase(TestName = "Expression Parser Should Synthesize Missing Not Operand At EOF")]
    public void ParserShould_8()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not");

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

    [TestCase(TestName = "Expression Parser Should Preserve Unknown Recovery Elements After Not")]
    public void ParserShould_9()
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression("not $ true");

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

    [TestCase("notable", LexTokenType.Identifier, TestName = "Expression Parser Should Not Treat Notable As Not")]
    [TestCase("nottrue", LexTokenType.Identifier, TestName = "Expression Parser Should Not Split Nottrue Into Keyword And Literal")]
    [TestCase("True", LexTokenType.Identifier, TestName = "Expression Parser Should Preserve Case Sensitive Boolean Literals")]
    [TestCase("@not", LexTokenType.EscapedIdentifier, TestName = "Expression Parser Should Not Treat Escaped Not As Operator")]
    [TestCase("@true", LexTokenType.EscapedIdentifier, TestName = "Expression Parser Should Not Treat Escaped True As Literal")]
    public void ParserShould_10(string source, LexTokenType expectedType)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);

        IdentifierExpressionSyntax identifier = expression
            .Should()
            .BeOfType<IdentifierExpressionSyntax>()
            .Subject;

        identifier.IdentifierToken.IsSourceBacked
            .Should()
            .BeTrue();

        identifier.IdentifierToken.SourceToken!.Value.Type
            .Should()
            .Be(expectedType);

        identifier.IdentifierToken.Type
            .Should()
            .Be(expectedType is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken);

        cursor.TryPeek(out _)
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true and false", "and", SyntaxType.AndKeyword, TestName = "Expression Parser Should Recognize Logical And Keyword")]
    [TestCase("not false or true", "or", SyntaxType.OrKeyword, TestName = "Expression Parser Should Recognize Logical Or Keyword")]
    public void ParserShould_11(string source, string expectedKeyword, SyntaxType expectedType)
    {
        (ExpressionSyntax expression, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseExpression(source);
    
        BinaryExpressionSyntax binary = expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;
    
        binary.OperatorToken.Type
            .Should()
            .Be(expectedType);
    
        binary.OperatorToken.SourceToken!.Value.Type
            .Should()
            .Be(LexTokenType.Keyword);
    
        GetSpelling(binary.OperatorToken)
            .Should()
            .Be(expectedKeyword);
    
        cursor.TryPeek(out _)
            .Should()
            .BeFalse();
    
        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true", TestName = "Executable MVP Should Diagnose Unsupported Boolean Literal")]
    [TestCase("not false", TestName = "Executable MVP Should Diagnose Unsupported Logical Negation")]
    public void ParserShould_12(string expression)
    {
        string source = $"public int32 main() {{ return {expression}; }}";

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

    [TestCase(TestName = "Expression Parser Should Honor Cancellation Before Logical Negation")]
    public void ParserShould_13()
    {
        using CancellationTokenSource cancellationTokenSource = new();

        ParserCursor cursor = new(Lex("not true"), cancellationTokenSource.Token);
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

    [TestCase(TestName = "Expression Parser Should Preserve UTF8 Byte Offsets For Boolean Expressions")]
    public void ParserShould_14()
    {
        LexerResult lexerResult = Lex("雪 not true");
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

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return lexerResult;
    }
}