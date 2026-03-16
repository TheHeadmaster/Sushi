using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

/// <summary>
/// Handles the parsing of do while loops.
/// </summary>
public sealed class DoWhileParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Do];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Do);

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek());

        if (body is not BlockNode block)
        {
            return null;
        }

        await parser.ExpectAndPop(TokenType.While);

        Token? nextToken = await parser.PeekAndExpectNotEOF();

        ExpressionNode? condition = await parser.ParseExpression(BindingPower.Primary);

        return new DoWhileNode(token, condition, block);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
