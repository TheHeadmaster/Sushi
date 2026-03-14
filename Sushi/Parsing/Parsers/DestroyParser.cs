using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of the destroy statement.
/// </summary>
public class DestroyParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Destroy];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Destroy);

        Token? currentToken = await parser.PeekAndExpectNotEOF();

        IdentifierNode obj = new(currentToken);

        await parser.ExpectAndPop(TokenType.Identifier);

        currentToken = parser.Peek();

        ExpressionNode? expression = currentToken is not null
            && currentToken.Type is TokenType.Identifier
            ? await parser.ParseExpression(BindingPower.Primary)
            : null;

        return new DestroyNode(token, obj, expression);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
