using FluentAssertions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing;

public sealed class UsingParserTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A Using Statement")]
    public async Task ParserShouldEmit_0()
    {
        this.SourceFile.Tokens.AddRange
        ([
            DummyToken(TokenType.Using),
            DummyToken(TokenType.Identifier, "System"),
            DummyToken(TokenType.Terminator)
        ]);

        AbstractSyntaxTree ast = await Parser.ParseSource([this.SourceFile]);

        ast.Children.Should().HaveCount(1);
        ast.Children[0].Statements.Should().HaveCount(1);
        ast.Children[0].Statements[0].Should().BeOfType<UsingNode>();

        UsingNode usingNode = (UsingNode)ast.Children[0].Statements[0];

        usingNode.Identifier.Should().NotBeNull();
        usingNode.Identifier.Should().BeOfType<IdentifierNode>();

        IdentifierNode identifier = (IdentifierNode)usingNode.Identifier;

        identifier.Name.Should().Be("System");
    }

    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A Using Statement With Navigation Operators")]
    public async Task ParserShouldEmit_1()
    {
        this.SourceFile.Tokens.AddRange
        ([
            DummyToken(TokenType.Using),
            DummyToken(TokenType.Identifier, "System"),
            DummyToken(TokenType.Dot),
            DummyToken(TokenType.Identifier, "Text"),
            DummyToken(TokenType.Terminator)
        ]);

        AbstractSyntaxTree ast = await Parser.ParseSource([this.SourceFile]);

        ast.Children.Should().HaveCount(1);
        ast.Children[0].Statements.Should().HaveCount(1);
        ast.Children[0].Statements[0].Should().BeOfType<UsingNode>();

        UsingNode usingNode = (UsingNode)ast.Children[0].Statements[0];

        usingNode.Identifier.Should().NotBeNull();
        usingNode.Identifier.Should().BeOfType<NamespaceNode>();

        NamespaceNode @namespace = (NamespaceNode)usingNode.Identifier;

        @namespace.Name.Name.Should().Be("System");

        @namespace.Right.Should().NotBeNull();

        @namespace.Right.Should().BeOfType<IdentifierNode>();

        IdentifierNode identifier = (IdentifierNode)@namespace.Right;

        identifier.Name.Should().Be("Text");
    }
}
