using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

/// <summary>
/// Handles the parsing of parameter lists.
/// </summary>
public sealed class ParameterListParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.OpeningParenthesis];

    public List<ParserRole> Roles { get; } = [ParserRole.ParameterList];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningParenthesis);

        Token? currentToken;

        List<ParameterNode> statements = [];

        while ((currentToken = parser.Peek()) is not null && currentToken.Type is not TokenType.ClosingParenthesis)
        {
            if (await parser.ParseStatement(currentToken, ParserRole.Parameter) is { } statement)
            {
                statements.Add((ParameterNode)statement);
            }

            if ((currentToken = parser.Peek()) is null || currentToken.Type is not TokenType.Comma)
            {
                break;
            }

            await parser.ExpectAndPop(TokenType.Comma);
        }

        await parser.ExpectAndPop(TokenType.ClosingParenthesis);

        ParameterListNode node = new(token, statements);

        return node;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
