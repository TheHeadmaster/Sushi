using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles member declarations, such as method declarations and fields/properties.
/// </summary>
public class MemberDeclarationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Identifier];

    public List<ParserRole> Roles { get; } = [ParserRole.MemberDeclaration];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = token;

        await parser.ExpectAndPop(TokenType.Identifier);

        TypeNode typeNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifierNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        if (currentToken?.Type is TokenType.Terminator)
        {
            await parser.ExpectAndPop(TokenType.Terminator);

            return new MemberDeclarationNode(token, typeNode, identifierNode);
        }

        await parser.ExpectAndPop(TokenType.OpeningParenthesis);

        while ((currentToken = await parser.PeekAndExpectNotEOF()) is not null)
        {
            TypeNode typeNode = new(currentToken)
        }

        await parser.ExpectAndPop(TokenType.ClosingParenthesis);

        BlockNode? block = (BlockNode?)await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek()!);

        return new MethodDeclarationNode(token, typeNode, identifierNode, parameters, block);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}