using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of the static modifier.
/// </summary>
public class StaticParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Static];

    public List<ParserRole> Roles { get; } = [ParserRole.TopLevelStatement];

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? staticToken = await parser.ExpectAndPop(TokenType.Static);

        if (staticToken is null)
        {
            return null;
        }

        StatementNode? right = await parser.ParseStatement(parser.Peek()!, ParserRole.StaticModifier);

        if (right is not ICanBeStatic staticNode)
        {
            return null;
        }

        staticNode.IsStatic = true;

        return right;
    }
}
