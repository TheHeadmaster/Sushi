using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of infix operators, such as addition and multiplication operators.
/// </summary>
public class InfixOperatorParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Infix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } =
    [
        TokenType.Plus,
        TokenType.Minus,
        TokenType.Asterisk,
        TokenType.Slash
    ];

    /// <summary>
    /// Maps each token to a binding power.
    /// </summary>
    private static readonly ReadOnlyDictionary<TokenType, BindingPower> infixBindingPowers = new(
        new Dictionary<TokenType, BindingPower>()
    {
        { TokenType.Plus, BindingPower.SumDifference },
        { TokenType.Minus, BindingPower.SumDifference },
        { TokenType.Asterisk, BindingPower.ProductQuotient },
        { TokenType.Slash, BindingPower.ProductQuotient },
    });

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token)
    {
        ExpressionNode? right = await parser.ParseExpression(infixBindingPowers[token.Type]);
        return new BinaryExpressionNode(token, left, right);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => infixBindingPowers[type];
}