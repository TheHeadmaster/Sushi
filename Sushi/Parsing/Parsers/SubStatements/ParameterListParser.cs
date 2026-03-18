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

    public List<ParserRole> Roles { get; } = [ParserRole.Parameter];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningParenthesis);

        Token? currentToken;

        List<S> statements = [];

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
    public BindingPower power(TokenType type) => BindingPower.Primary;
}
