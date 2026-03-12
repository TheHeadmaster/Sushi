using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class IfParser : IStatementParser
{
    public async Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.If);

        Token? nextToken = parser.Peek() ?? throw new NotImplementedException();

        ExpressionNode condition = await parser.ParseExpression(BindingPower.Primary);

        await parser.ExpectAndPop(TokenType.Then);

        BlockNode blockNode = await BlockParser.Parse(parser, parser.Peek());
        nextToken = parser.Peek();

        IfNode? elseNode = null;

        if (nextToken is not null && nextToken.Type is TokenType.Else)
        {
            elseNode = await this.ParseElseIfOrElse(parser, nextToken);
        }

        return new IfNode(token, condition, blockNode, elseNode);
    }

    private async Task<IfNode> ParseElseIfOrElse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Else);

        Token? nextToken = parser.Peek() ?? throw new NotImplementedException();

        ExpressionNode? condition = null;

        if (nextToken.Type is TokenType.If)
        {
            parser.Pop();
            condition = await parser.ParseExpression(BindingPower.Primary);
            await parser.ExpectAndPop(TokenType.Then);
            nextToken = parser.Peek() ?? throw new NotImplementedException();
        }

        BlockNode blockNode = await BlockParser.Parse(parser, nextToken);
        nextToken = parser.Peek();

        IfNode? elseNode = null;

        if (nextToken is not null && nextToken.Type is TokenType.Else)
        {
            elseNode = (IfNode)await this.ParseElseIfOrElse(parser, nextToken);
        }

        return new IfNode(token, condition, blockNode, elseNode);
    }
}
