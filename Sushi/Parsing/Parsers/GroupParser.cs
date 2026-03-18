using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles parsing of arbitrary grouping parenthesis, which is used to override the default precedence.
/// </summary>
public class GroupParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.OpeningParenthesis];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.OpeningParenthesis);

        ExpressionNode? expression = await parser.ParseExpression(BindingPower.Primary);

        await parser.ExpectAndPop(TokenType.ClosingParenthesis);

        return expression;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
