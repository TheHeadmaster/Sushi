using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public static class FieldDeclarationParser
{
    public static async Task<FieldDeclarationNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = token;

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode typeNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifierNode = new(currentToken);

        await parser.ExpectAndPop(TokenType.Terminator);

        return new FieldDeclarationNode(token, typeNode, identifierNode);
    }
}
