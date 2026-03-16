using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers;
using Sushi.Parsing.Parsers.TopLevelStatements;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing.TopLevelStatements;

public sealed class NamespaceDeclarationParserTests : ParsingTest
{
    [TestCase(TestName = "Parser Should Emit Proper AST When Parsing a Namespace Declaration Statement")]
    public async Task ParserShouldEmit_0()
    {
        await Parser.UseSnippet
        ([
            DummyToken(TokenType.Namespace),
            DummyToken(TokenType.Identifier, "System"),
            DummyToken(TokenType.Terminator)
        ]);

        IParser namespaceParser = Parser.GetParser<NamespaceDeclarationParser>();

        StatementNode? namespaceStatement = await namespaceParser.ParseStatement(Parser, Parser.Peek()!);

        namespaceStatement.Should().NotBeNull();
        namespaceStatement.Should().BeOfType<NamespaceDeclarationNode>();

        NamespaceDeclarationNode namespaceNode = (NamespaceDeclarationNode)namespaceStatement;

        namespaceNode.Body.Should().NotBeNull();
        namespaceNode.Body.Should().BeOfType<IdentifierNode>();

        IdentifierNode identifierNode = (IdentifierNode)namespaceNode.Body;

        identifierNode.Name.Should().Be("System");
    }
}
