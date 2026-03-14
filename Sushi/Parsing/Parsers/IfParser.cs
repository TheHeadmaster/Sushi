using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of if then else statements.
/// </summary>
public class IfParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.If];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.If);

        Token? nextToken = await parser.PeekAndExpectNotEOF();

        ExpressionNode? condition = await parser.ParseExpression(BindingPower.Primary);

        await parser.ExpectAndPop(TokenType.Then);

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek());

        if (body is not BlockNode block)
        {
            return null;
        }

        nextToken = parser.Peek();

        IfNode? elseNode = null;

        if (nextToken is not null && nextToken.Type is TokenType.Else)
        {
            elseNode = await ParseElseIfOrElse(parser, nextToken);
        }

        return new IfNode(token, condition, block, elseNode);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;

    /// <summary>
    /// Parses the else if or else branches of an if statement.
    /// </summary>
    /// <param name="parser">The main parser.</param>
    /// <param name="token">The <see cref="Token"/> to parse.</param>
    /// <returns>An <see cref="IfNode"/> or null if there isn't an else if or else branch.</returns>
    private static async Task<IfNode?> ParseElseIfOrElse([NotNull] Parser parser, [NotNull] Token token)
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

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, nextToken);

        if (body is not BlockNode block)
        {
            return null;
        }

        nextToken = parser.Peek();

        IfNode? elseNode = null;

        if (nextToken is not null && nextToken.Type is TokenType.Else)
        {
            elseNode = await ParseElseIfOrElse(parser, nextToken);
        }

        return new IfNode(token, condition, block, elseNode);
    }
}
