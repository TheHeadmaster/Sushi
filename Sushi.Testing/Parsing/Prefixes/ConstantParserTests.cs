using FluentAssertions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers;
using Sushi.Parsing.Parsers.Prefixes;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing.Prefixes;

public sealed class ConstantParserTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing An Integer Constant")]
    public async Task ParserShouldEmit_0()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.NumberLiteral, "5")
        ]);

        IParser constantParser = Parser.GetParser<ConstantParser>();

        ExpressionNode? constantExpression = await constantParser.ParsePrefix(Parser, Parser.Peek()!);

        constantExpression.Should().NotBeNull();
        constantExpression.Should().BeOfType<ConstantNode>();

        ConstantNode constant = (ConstantNode)constantExpression;

        constant.Value.Should().Be("5");
    }

    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A Real Number Constant")]
    public async Task ParserShouldEmit_1()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.NumberLiteral, "5.4")
        ]);

        IParser constantParser = Parser.GetParser<ConstantParser>();

        ExpressionNode? constantExpression = await constantParser.ParsePrefix(Parser, Parser.Peek()!);

        constantExpression.Should().NotBeNull();
        constantExpression.Should().BeOfType<ConstantNode>();

        ConstantNode constant = (ConstantNode)constantExpression;

        constant.Value.Should().Be("5.4");
    }

    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A True Literal Constant")]
    public async Task ParserShouldEmit_2()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.NumberLiteral, "true")
        ]);

        IParser constantParser = Parser.GetParser<ConstantParser>();

        ExpressionNode? constantExpression = await constantParser.ParsePrefix(Parser, Parser.Peek()!);

        constantExpression.Should().NotBeNull();
        constantExpression.Should().BeOfType<ConstantNode>();

        ConstantNode constant = (ConstantNode)constantExpression;

        constant.Value.Should().Be("true");
    }

    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A False Literal Constant")]
    public async Task ParserShouldEmit_3()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.NumberLiteral, "false")
        ]);

        IParser constantParser = Parser.GetParser<ConstantParser>();

        ExpressionNode? constantExpression = await constantParser.ParsePrefix(Parser, Parser.Peek()!);

        constantExpression.Should().NotBeNull();
        constantExpression.Should().BeOfType<ConstantNode>();

        ConstantNode constant = (ConstantNode)constantExpression;

        constant.Value.Should().Be("false");
    }
}
