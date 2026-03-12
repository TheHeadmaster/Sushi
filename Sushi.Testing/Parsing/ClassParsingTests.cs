using FluentAssertions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing;

public sealed class ClassParsingTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing An Instanced Class")]
    public async Task ParserShouldEmit_0()
    {
        this.SourceFile.Tokens.AddRange
        ([
            DummyToken(TokenType.Class),
            DummyToken(TokenType.Identifier, "SomeClass"),
            DummyToken(TokenType.OpeningSquiggly),
            DummyToken(TokenType.ClosingSquiggly)
        ]);

        AbstractSyntaxTree ast = await Parser.ParseSource([this.SourceFile]);

        ast.Children.Should().HaveCount(1);
        ast.Children[0].Statements.Should().HaveCount(1);
        ast.Children[0].Statements[0].Should().BeOfType<ClassNode>();

        ClassNode classNode = (ClassNode)ast.Children[0].Statements[0];

        classNode.IsStatic.Should().BeFalse();

        classNode.Name.Should().NotBeNull();
        classNode.Name.Name.Should().Be("SomeClass");

        classNode.Body.Should().NotBeNull();
        classNode.Body.Body.Should().NotBeNull();
        classNode.Body.Body.Count.Should().Be(0);
    }

    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing A Static Class")]
    public async Task ParserShouldEmit_1()
    {
        this.SourceFile.Tokens.AddRange
        ([
            DummyToken(TokenType.Static),
            DummyToken(TokenType.Class),
            DummyToken(TokenType.Identifier, "Monolith"),
            DummyToken(TokenType.OpeningSquiggly),
            DummyToken(TokenType.ClosingSquiggly)
        ]);

        AbstractSyntaxTree ast = await Parser.ParseSource([this.SourceFile]);

        ast.Children.Should().HaveCount(1);
        ast.Children[0].Statements.Should().HaveCount(1);
        ast.Children[0].Statements[0].Should().BeOfType<ClassNode>();

        ClassNode classNode = (ClassNode)ast.Children[0].Statements[0];

        classNode.IsStatic.Should().BeTrue();

        classNode.Name.Should().NotBeNull();
        classNode.Name.Name.Should().Be("Monolith");

        classNode.Body.Should().NotBeNull();
        classNode.Body.Body.Should().NotBeNull();
        classNode.Body.Body.Count.Should().Be(0);
    }
}
