using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
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
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Public, TokenType.Internal, TokenType.Private];

    /// <inheritdoc />
    public List<ParserRole> Roles { get; } = [ParserRole.TopLevelStatement, ParserRole.MemberDeclaration];

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? accessToken = await parser.ExpectAndPop([.. this.AllowedStartTokens]);

        if (accessToken is null)
        {
            return null;
        }

        Token? nextToken = await parser.PeekAndExpectNotEOF();

        if (nextToken is null)
        {
            return null;
        }

        StatementNode? right = await parser.ParseStatement(nextToken, ParserRole.AccessModifier);

        if (right is null)
        {
            return null;
        }

        AccessModifier modifier = accessToken.Type switch
        {
            TokenType.Public => AccessModifier.Public,
            TokenType.Internal => AccessModifier.Internal,
            TokenType.Private => AccessModifier.Private,
            _ => throw new NotImplementedException(),
        };

        if (right is IAccessModifiable accessNode && accessNode.AllowsModifier(modifier))
        {
            accessNode.AccessModifier = modifier;
        }
        else
        {
            await right.AddMessage(new IllegalAccessModifierError(token, parser.CurrentFileNode.FilePath));
        }

        return right;
    }
}