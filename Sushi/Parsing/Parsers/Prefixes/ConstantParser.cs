using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.Prefixes;

/// <summary>
/// Handles the parsing of constants, such as 3 or true.
/// </summary>
public sealed class ConstantParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.NumberLiteral, TokenType.TrueLiteral, TokenType.FalseLiteral];

    /// <inheritdoc />
    public Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<ExpressionNode?>(new ConstantNode(token));

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
