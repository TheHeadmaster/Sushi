using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.Prefixes;

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
    public async Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token)
    {
        parser.Pop();
        return new IdentifierNode(token, parser.CurrentFileNode.FilePath);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}