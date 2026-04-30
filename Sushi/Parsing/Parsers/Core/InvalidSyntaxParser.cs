using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.Core;

/// <summary>
/// Handles the parsing of invalid syntax nodes.
/// </summary>
public sealed class InvalidSyntaxParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Unknown];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop([.. this.AllowedStartTokens]);
        return new InvalidSyntaxNode(token);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
