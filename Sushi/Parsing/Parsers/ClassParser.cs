using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public static class ClassParser
{
    public static async Task<ClassNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        bool isStatic = false;

        Token? currentToken = token;

        if (currentToken.Type is TokenType.Static)
        {
            isStatic = true;
            parser.Pop();
        }

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Class);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifier = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        BlockNode body = await BlockParser.Parse(parser, currentToken);

        ClassNode classNode = new(token, identifier, body, isStatic);

        return classNode;
    }
}
