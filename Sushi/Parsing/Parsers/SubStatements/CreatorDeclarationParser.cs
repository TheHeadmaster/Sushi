using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

/// <summary>
/// Handles the parsing of the creator statement.
/// </summary>
public class CreatorDeclarationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Creator];

    public List<ParserRole> Roles { get; } = [ParserRole.MemberDeclaration];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = token;

        await parser.ExpectAndPop(TokenType.Creator);

        currentToken = await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifierNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        ParameterListNode? parameterList = (ParameterListNode?)await parser.ParseStatement(currentToken, ParserRole.ParameterList);

        BlockNode? block = (BlockNode?)await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek()!);

        return new CreatorDeclarationNode(token, identifierNode, parameterList, block);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
