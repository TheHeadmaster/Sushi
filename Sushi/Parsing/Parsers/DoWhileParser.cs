using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class DoWhileParser : IStatementParser
{
    public async Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Do);

        BlockNode blockNode = await BlockParser.Parse(parser, parser.Peek());

        await parser.ExpectAndPop(TokenType.While);

        Token nextToken = await parser.PeekAndExpectNotEOF();

        ExpressionNode condition = await parser.ParseExpression(BindingPower.Primary);

        return new DoWhileNode(token, condition, blockNode);
    }
}
