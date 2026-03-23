using FluentAssertions;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing.SubStatements;

public sealed class WhileParserTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A While Statement")]
    public async Task ParserShouldEmit_0()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.While),
            DummyToken(TokenType.TrueLiteral, "true"),
            DummyToken(TokenType.Do),
            DummyToken(TokenType.OpeningSquiggly),
            DummyToken(TokenType.Identifier, "amount"),
            DummyToken(TokenType.Assignment),
            DummyToken(TokenType.Identifier, "amount"),
            DummyToken(TokenType.Plus),
            DummyToken(TokenType.NumberLiteral, "5"),
            DummyToken(TokenType.Terminator),
            DummyToken(TokenType.ClosingSquiggly),
        ]);

        IParser whileParser = Parser.GetParser<WhileParser>();

        StatementNode? whileStatement = await whileParser.ParseStatement(Parser, Parser.Peek()!);

        whileStatement.Should().NotBeNull();

        WhileNode whileNode = (WhileNode)whileStatement;

        whileNode.Condition.Should().NotBeNull();
        whileNode.Condition.Should().BeOfType<ConstantNode>();

        ConstantNode constant = (ConstantNode)whileNode.Condition;

        constant.Value.Should().Be("true");

        whileNode.Body.Should().NotBeNull();
        whileNode.Body.Should().BeOfType<BlockNode>();

        BlockNode block = whileNode.Body;

        whileNode.Body.Statements.Should().HaveCount(1);
        whileNode.Body.Statements[0].Should().BeOfType<ExpressionStatementNode>();

        ExpressionStatementNode statement = (ExpressionStatementNode)block.Statements[0];

        statement.Expression.Should().NotBeNull();
        statement.Expression.Should().BeOfType<AssignmentNode>();

        AssignmentNode assignment = (AssignmentNode)statement.Expression;

        assignment.Identifier.Name.Should().Be("amount");

        assignment.Right.Should().NotBeNull();
        assignment.Right.Should().BeOfType<BinaryExpressionNode>();

        BinaryExpressionNode addition = (BinaryExpressionNode)assignment.Right;

        addition.Left.Should().NotBeNull();
        addition.Left.Should().BeOfType<IdentifierNode>();
        addition.Right.Should().NotBeNull();
        addition.Right.Should().BeOfType<ConstantNode>();

        IdentifierNode identifier = (IdentifierNode)addition.Left;

        ConstantNode constant2 = (ConstantNode)addition.Right;

        identifier.Name.Should().Be("amount");

        constant2.Value.Should().Be("5");
    }
}
