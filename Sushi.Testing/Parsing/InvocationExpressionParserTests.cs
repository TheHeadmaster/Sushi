using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

/// <summary>
/// Verifies that invocation syntax is preserved independently of callable resolution and return typing.
/// </summary>
[TestFixture]
public class InvocationExpressionParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Invocations.sus");

    [TestCase(TestName = "Invocation Parser Should Recognize Parameterless Statement")]
    public void ParserShould_0()
    {
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Initialize(); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Target
            .Should()
            .BeOfType<IdentifierExpressionSyntax>();

        invocation.Arguments
            .Should()
            .BeEmpty();

        invocation.ArgumentSeparators
            .Should()
            .BeEmpty();

        invocation.OpenParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        invocation.CloseParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "Invocation Parser Should Preserve Positional And Named Arguments")]
    public void ParserShould_1()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Configure(42, enabled: true); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Arguments
            .Should()
            .HaveCount(2);

        invocation.ArgumentSeparators
            .Should()
            .ContainSingle();

        invocation.Arguments[0].IsNamed
            .Should()
            .BeFalse();

        invocation.Arguments[0].Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        invocation.Arguments[1].IsNamed
            .Should()
            .BeTrue();

        invocation.Arguments[1].Name
            .Should()
            .NotBeNull();

        invocation.Arguments[1].Expression
            .Should()
            .BeOfType<BooleanLiteralExpressionSyntax>();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Preserve Nested Calls In Source Order")]
    public void ParserShould_2()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Process(GetValue(), retry: NextFlag()); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Arguments
            .Should()
            .HaveCount(2);

        InvocationExpressionSyntax first = invocation.Arguments[0].Expression
            .Should()
            .BeOfType<InvocationExpressionSyntax>()
            .Subject;

        InvocationExpressionSyntax second = invocation.Arguments[1].Expression
            .Should()
            .BeOfType<InvocationExpressionSyntax>()
            .Subject;

        first.Span.Start
            .Should()
            .BeLessThan(second.Span.Start);

        invocation.Arguments[1].IsNamed
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Recognize Calls In Return Expressions")]
    public void ParserShould_3()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ return Compute(1 + 2); }");

        ReturnStatementSyntax statement = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        InvocationExpressionSyntax invocation = statement.Expression
            .Should()
            .BeOfType<InvocationExpressionSyntax>()
            .Subject;

        invocation.Arguments
            .Should()
            .ContainSingle();

        invocation.Arguments[0].Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Recognize Calls In Conditional Statements")]
    public void ParserShould_4()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ if (IsReady()) { Execute(); } }");

        IfStatementSyntax conditional = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IfStatementSyntax>()
            .Subject;

        conditional.Condition
            .Should()
            .BeOfType<InvocationExpressionSyntax>();

        ExpressionStatementSyntax statement = conditional.ThenBlock.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ExpressionStatementSyntax>()
            .Subject;

        statement.Expression
            .Should()
            .BeOfType<InvocationExpressionSyntax>();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Preserve Binary Expression Statement")]
    public void ParserShould_5()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Compute() + 1; }");

        ExpressionStatementSyntax statement = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ExpressionStatementSyntax>()
            .Subject;

        BinaryExpressionSyntax expression = statement.Expression
            .Should()
            .BeOfType<BinaryExpressionSyntax>()
            .Subject;

        expression.Left
            .Should()
            .BeOfType<InvocationExpressionSyntax>();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Preserve Escaped Named Argument")]
    public void ParserShould_6()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Configure(@default: 42); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Arguments[0].Name
            .Should()
            .NotBeNull();

        SyntaxToken label = invocation.Arguments[0].Name!.Value;

        label.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Preserve Argument Ordering Without Resolving Applicability")]
    public void ParserShould_7()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Configure(width: 42, 10); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Arguments
            .Should()
            .HaveCount(2);

        invocation.Arguments[0].IsNamed
            .Should()
            .BeTrue();

        invocation.Arguments[1].IsNamed
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Invocation Parser Should Recover Missing Expression After Comma")]
    public void ParserShould_8()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Execute(1,); }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.Arguments
            .Should()
            .HaveCount(2);

        IntegerLiteralExpressionSyntax missing = invocation.Arguments[1].Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        missing.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        invocation.CloseParenthesisToken.IsMissing
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Invocation Parser Should Recover Missing Closing Parenthesis")]
    public void ParserShould_9()
    {
        (BlockSyntax block, _, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ Execute(42; }");

        InvocationExpressionSyntax invocation = GetSingleInvocation(block);

        invocation.CloseParenthesisToken.IsMissing
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    private static InvocationExpressionSyntax GetSingleInvocation(BlockSyntax block)
    {
        ExpressionStatementSyntax statement = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ExpressionStatementSyntax>()
            .Subject;

        return statement.Expression
            .Should()
            .BeOfType<InvocationExpressionSyntax>()
            .Subject;
    }

    private static (BlockSyntax Block, ParserCursor Cursor, IReadOnlyList<SushiDiagnostic> Diagnostics) ParseBlock(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter lexerDiagnostics = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, lexerDiagnostics, CancellationToken.None);

        lexerDiagnostics.ReportDiagnostics()
            .Should()
            .BeEmpty();

        ParserCursor cursor = new(lexerResult, CancellationToken.None);
        StatementParser parser = new(cursor, CancellationToken.None);
        IDiagnosticReporter parserDiagnostics = new DiagnosticReporter();
        BlockSyntax block = parser.ParseBlock(parserDiagnostics);

        return (block, cursor, parserDiagnostics.ReportDiagnostics());
    }
}