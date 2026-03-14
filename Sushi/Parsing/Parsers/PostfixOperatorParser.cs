using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Parses postfix operators, such as increment.
/// </summary>
public sealed class PostfixOperatorParser : IParser
{
    /// <inheritdoc />
    public ParserType Type => ParserType.Infix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens => [];

    /// <inheritdoc />
    public Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token)
        => Task.FromResult<ExpressionNode?>(new UnaryExpressionNode(token, false, left));

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Postfix;
}
