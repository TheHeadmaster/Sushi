using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of identifiers, which are names of variables, parameters, and members.
/// </summary>
public class IdentifierParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Identifier];

    /// <inheritdoc />
    public Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<ExpressionNode?>(new IdentifierNode(token));

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
