using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

/// <summary>
/// Handles the parsing of block statements.
/// </summary>
public sealed class BlockParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.OpeningSquiggly];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningSquiggly);

        Token? currentToken;

        List<StatementNode> statements = [];

        while ((currentToken = parser.Peek()) is not null && currentToken.Type is not TokenType.ClosingSquiggly)
        {
            if (await parser.ParseStatement(currentToken, ParserRole.BlockStatement) is { } statement)
            {
                statements.Add(statement);
            }
        }

        await parser.ExpectAndPop(TokenType.ClosingSquiggly);

        BlockNode block = new(token, statements);

        return block;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
