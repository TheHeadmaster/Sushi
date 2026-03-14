using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of prefix operators, such as the negative sign.
/// </summary>
public sealed class PrefixOperatorParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Minus];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token)
    {
        ExpressionNode? operand = await parser.ParseExpression(BindingPower.Prefix);

        return new UnaryExpressionNode(token, true, operand);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Prefix;
}
