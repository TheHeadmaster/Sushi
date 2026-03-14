using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of while loops.
/// </summary>
public class WhileParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.While];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.While);

        Token? nextToken = await parser.PeekAndExpectNotEOF();

        ExpressionNode? condition = await parser.ParseExpression(BindingPower.Primary);

        await parser.ExpectAndPop(TokenType.Do);

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek());

        if (body is not BlockNode block)
        {
            return null;
        }

        return new WhileNode(token, condition, block);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
