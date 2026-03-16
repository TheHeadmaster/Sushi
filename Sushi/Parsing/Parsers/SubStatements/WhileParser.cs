using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

/// <summary>
/// Handles the parsing of while loops.
/// </summary>
public sealed class WhileParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.While];

    /// <inheritdoc />
    public List<ParserRole> Roles { get; } = [ParserRole.BlockStatement];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.While);

        await parser.PeekAndExpectNotEOF();

        ExpressionNode? condition = await parser.ParseExpression(BindingPower.Primary);

        await parser.ExpectAndPop(TokenType.Do);

        Token? statementToken = await parser.PeekAndExpectNotEOF();

        if (statementToken is null)
        {
            return new WhileNode(token, condition, null);
        }

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, statementToken);

        if (body is not BlockNode block)
        {
            return new WhileNode(token, condition, null);
        }

        return new WhileNode(token, condition, block);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
