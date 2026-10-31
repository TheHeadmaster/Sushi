
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

/// <summary>
/// Verifies parsing and recovery for braced conditional statements.
/// </summary>
[TestFixture]
public class IfStatementParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/IfStatements.sus");

    [TestCase(TestName = "If Parser Should Preserve Else If Chain And Final Else")]
    public void ParserShould_0()
    {
        const string source = "{ if (true) { return; } else if (false) { return; } else { return; } }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        IfStatementSyntax conditional = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IfStatementSyntax>()
            .Subject;

        conditional.ThenBlock.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ReturnStatementSyntax>();

        conditional.ElseBlock
            .Should()
            .BeNull();

        conditional.ElseIf
            .Should()
            .NotBeNull();

        IfStatementSyntax elseIf = conditional.ElseIf!;

        elseIf.ThenBlock.Statements
            .Should()
            .ContainSingle();

        elseIf.ElseBlock
            .Should()
            .NotBeNull();

        elseIf.ElseIf
            .Should()
            .BeNull();

        conditional.Span.Start
            .Should()
            .Be(source.IndexOf("if", StringComparison.Ordinal));

        conditional.Span.End
            .Should()
            .Be(source.LastIndexOf('}', source.Length - 2) + 1);

        diagnostics
            .Should()
            .BeEmpty();

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "If Parser Should Preserve Following Statements")]
    public void ParserShould_1()
    {
        const string source = "{ if (true) {} return; }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        block.Statements
            .Should()
            .HaveCount(2);

        block.Statements[0]
            .Should()
            .BeOfType<IfStatementSyntax>();

        block.Statements[1]
            .Should()
            .BeOfType<ReturnStatementSyntax>();

        diagnostics
            .Should()
            .BeEmpty();

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "If Parser Should Report Missing Condition Parenthesis")]
    public void ParserShould_2()
    {
        const string source = "{ if (true { return; } }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        IfStatementSyntax conditional = block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IfStatementSyntax>()
            .Subject;

        conditional.CloseParenthesisToken.IsMissing
            .Should()
            .BeTrue();

        conditional.ThenBlock.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        cursor.Position
            .Should()
            .Be(cursor.Snapshot.SourceLength);
    }

    [TestCase(TestName = "If Parser Should Reject Unbraced True Branch")]
    public void ParserShould_3()
    {
        const string source = "{ if (true) return; }";
        (BlockSyntax block, ParserCursor cursor, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseBlock(source);

        block.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IfStatementSyntax>();

        diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        cursor.Position
            .Should()
            .BeGreaterThan(0);
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