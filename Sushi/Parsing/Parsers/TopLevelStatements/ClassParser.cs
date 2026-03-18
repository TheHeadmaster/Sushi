using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.TopLevelStatements;

/// <summary>
/// Handles the parsing of class declarations.
/// </summary>
public sealed class ClassParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Class];

    /// <inheritdoc />
    public List<ParserRole> Roles { get; } = [ParserRole.TopLevelStatement, ParserRole.StaticModifier];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Class);

        Token? identifierToken = await parser.ExpectAndPop(TokenType.Identifier);

        TypeNode? identifier = identifierToken is null ? null : new(identifierToken);

        if (identifier is not null)
        {
            if (!await parser.Reference.TryAddType(identifier))
            {
                parser.Messages.Add(new TypeNameCollisionError(identifierToken!));
            }
        }

        await parser.ExpectAndPop(TokenType.OpeningSquiggly);

        List<StatementNode> statements = [];

        Token? statementToken;

        while ((statementToken = parser.Peek()) is { } && statementToken.Type is not TokenType.ClosingSquiggly)
        {
            StatementNode? statement = await parser.ParseStatement(statementToken, ParserRole.MemberDeclaration);

            if (statement is not null)
            {
                statements.Add(statement);
            }
        }

        await parser.ExpectAndPop(TokenType.ClosingSquiggly);

        ClassNode classNode = new(token, identifier, statements);

        return classNode;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
