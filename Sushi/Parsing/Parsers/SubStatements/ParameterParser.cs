using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.SubStatements;

public sealed class ParameterParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Identifier];

    public List<ParserRole> Roles { get; } = [ParserRole.Parameter];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token typeToken = await parser.ExpectAndPop(TokenType.Identifier);
        Token identifierToken = await parser.ExpectAndPop(TokenType.Identifier);

        TypeNode typeNode = new(typeToken);
        IdentifierNode identifierNode = new(identifierToken);

        return new ParameterNode(typeNode, identifierNode);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
