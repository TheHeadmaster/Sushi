using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public static class BlockParser
{
    public static async Task<BlockNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningSquiggly);

        Token? currentToken;

        List<StatementNode> statements = [];

        while ((currentToken = parser.Peek()) is not null && currentToken.Type is not TokenType.ClosingSquiggly)
        {
            statements.Add(await parser.ParseStatement(currentToken));
        }

        await parser.ExpectAndPop(TokenType.ClosingSquiggly);

        BlockNode block = new(token, statements);

        return block;
    }
}
