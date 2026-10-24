using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class StatementParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Statements.sus");

    [TestCase(TestName = "Statement Parser Should Parse Empty Block")]
    public void ParserShould_0()
    {
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock("{ /* empty */ }");

        diagnostics
            .Should()
            .BeEmpty();

        block.Statements
            .Should()
            .BeEmpty();

        block.OpenBraceToken.IsMissing
            .Should()
            .BeFalse();

        block.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "Statement Parser Should Parse Consecutive Return Statements")]
    public void ParserShould_1()
    {
        const string source = "{ return 1; return 42; }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        diagnostics
            .Should()
            .BeEmpty();

        block.Statements
            .Should()
            .HaveCount(2)
            .And.OnlyContain(statement => statement.Type == SyntaxType.ReturnStatement);

        ReturnStatementSyntax first = (ReturnStatementSyntax)block.Statements[0];
        ReturnStatementSyntax second = (ReturnStatementSyntax)block.Statements[1];

        first.Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>();

        second.Expression.Span.Start
            .Should()
            .Be(source.IndexOf("42", StringComparison.Ordinal));

        first.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        second.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "Statement Parser Should Preserve Closing Brace When Return Semicolon Is Missing")]
    public void ParserShould_2()
    {
        const string source = "{ return 42 }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        ReturnStatementSyntax statement = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        statement.SemicolonToken.IsMissing
            .Should()
            .BeTrue();

        statement.SemicolonToken.Span.Start
            .Should()
            .Be(source.IndexOf('}', StringComparison.Ordinal));

        block.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "Statement Parser Should Preserve Unsupported Statement As Unconsumed Source")]
    public void ParserShould_3()
    {
        const string source = "{ return 42; other; }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        block.Statements
            .Should()
            .ContainSingle();

        block.CloseBraceToken.IsMissing
            .Should()
            .BeTrue();

        cursor.Position
            .Should()
            .Be(source.IndexOf("other", StringComparison.Ordinal));

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Statement Parser Should Synthesize Closing Brace At EOF")]
    public void ParserShould_4()
    {
        const string source = "{ return 42;";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        block.CloseBraceToken.IsMissing
            .Should()
            .BeTrue();

        block.CloseBraceToken.Span.Start
            .Should()
            .Be(cursor.Snapshot.SourceLength);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Statement Parser Should Preserve Semicolon After Missing Return Expression")]
    public void ParserShould_5()
    {
        const string source = "{ return ; }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        ReturnStatementSyntax statement = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        IntegerLiteralExpressionSyntax literal = statement.Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        literal.LiteralToken.IsMissing
            .Should()
            .BeTrue();

        literal.Span.Start
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));

        statement.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        block.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
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
