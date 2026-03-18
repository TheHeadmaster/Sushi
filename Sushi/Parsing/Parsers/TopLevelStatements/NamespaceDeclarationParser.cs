using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.TopLevelStatements;

/// <summary>
/// Handles the parsing of namespace declaration statements.
/// </summary>
public sealed class NamespaceDeclarationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Namespace];

    /// <inheritdoc />
    public List<ParserRole> Roles { get; } = [ParserRole.TopLevelStatement];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Namespace);

        ExpressionNode? expression = await parser.ParseExpression(BindingPower.Primary);

        if (expression is not IdentifierNode and not NamespaceNode)
        {
            parser.Messages.Add(new InvalidNamespaceError(token, parser.Previous()!));
        }

        NamespaceDeclarationNode namespaceStatement = new(token, expression);

        await parser.ExpectAndPop(TokenType.Terminator);

        await parser.Reference.StartNamespace(namespaceStatement);

        return namespaceStatement;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
