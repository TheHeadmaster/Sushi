using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public static class MethodDeclarationParser
{
    public static async Task<MethodDeclarationNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = token;

        await parser.ExpectAndPop(TokenType.Identifier);

        TypeNode typeNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifierNode = new(currentToken);

        await parser.ExpectAndPop(TokenType.OpeningParenthesis);
        await parser.ExpectAndPop(TokenType.ClosingParenthesis);
        await parser.ExpectAndPop(TokenType.OpeningSquiggly);
        await parser.ExpectAndPop(TokenType.ClosingSquiggly);

        return new MethodDeclarationNode(token, typeNode, identifierNode);
    }
}
