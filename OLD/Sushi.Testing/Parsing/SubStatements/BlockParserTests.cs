using FluentAssertions;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing.SubStatements;

public sealed class BlockParserTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A Block Statement")]
    public async Task ParserShouldEmit_0()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.OpeningSquiggly),
            DummyToken(TokenType.Identifier, "amount"),
            DummyToken(TokenType.Assignment),
            DummyToken(TokenType.Identifier, "amount"),
            DummyToken(TokenType.Plus),
            DummyToken(TokenType.NumberLiteral, "5"),
            DummyToken(TokenType.Terminator),
            DummyToken(TokenType.ClosingSquiggly)
        ]);

        IParser blockParser = Parser.GetParser<BlockParser>();

        StatementNode? blockStatement = await blockParser.ParseStatement(Parser, Parser.Peek()!);

        blockStatement.Should().NotBeNull();
        blockStatement.Should().BeOfType<BlockNode>();

        BlockNode block = (BlockNode)blockStatement;

        block.Statements.Should().HaveCount(1);
        block.Statements[0].Should().BeOfType<ExpressionStatementNode>();

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
