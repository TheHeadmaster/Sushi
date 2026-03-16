using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of block statements.
/// </summary>
public class BlockParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.OpeningSquiggly];

    /// <inheritdoc />
    public static async Task<BlockNode> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningSquiggly);

        Token? currentToken;

        List<StatementNode> statements = [];

        while ((currentToken = parser.Peek()) is not null && currentToken.Type is not TokenType.ClosingSquiggly)
        {
            statements.Add(await parser.ParseStatement(currentToken, [Parser.GetParser<WhileParser>()]));
        }

        await parser.ExpectAndPop(TokenType.ClosingSquiggly);

        BlockNode block = new(token, statements);

        return block;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
