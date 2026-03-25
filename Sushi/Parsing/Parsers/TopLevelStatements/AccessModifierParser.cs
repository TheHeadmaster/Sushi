using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.TopLevelStatements;

/// <summary>
/// Handles parsing of access modifiers, such as "public", "private", or "internal".
/// </summary>
public class AccessModifierParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Public, TokenType.Internal];

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? accessToken = await parser.ExpectAndPop(TokenType.Public, TokenType.Internal);

        if (accessToken is null)
        {
            return null;
        }

        StatementNode? right = await parser.ParseStatement(token, ParserRole.AccessModifier);

        if (right is IAccessModifiable accessNode)
        {
            accessNode.AccessModifier = accessToken.Type switch
            {
                TokenType.Public => AccessModifier.Public,
                TokenType.Internal => AccessModifier.Internal,
                TokenType.Private => AccessModifier.Private,
                _ => throw new NotImplementedException(),
            };
        }
        else
        {
            parser.Messages.Add(new IllegalAccessModifierError(token));
        }

        return right;
    }
}
