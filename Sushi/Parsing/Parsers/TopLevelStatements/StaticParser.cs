using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.TopLevelStatements;

/// <summary>
/// Handles the parsing of the static modifier.
/// </summary>
public class StaticParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Static];

    public List<ParserRole> Roles { get; } = [ParserRole.AccessModifier];

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

        Token? nextToken = await parser.PeekAndExpectNotEOF();

        if (nextToken is null)
        {
            return null;
        }

        StatementNode? right = await parser.ParseStatement(nextToken, ParserRole.StaticModifier);

        if (right is null)
        {
            return null;
        }

        if (right is ICanBeStatic staticNode)
        {
            staticNode.IsStatic = true;
        }
        else
        {
            await right.AddMessage(new IllegalStaticModifierError(token, parser.CurrentFileNode.FilePath));
        }

        return right;
    }
}