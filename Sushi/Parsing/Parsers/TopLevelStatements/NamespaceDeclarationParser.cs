using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.TopLevelStatements;
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
        parser.Pop();

        ExpressionNode? expression = await parser.ParseExpression(BindingPower.Primary);

        Token? terminator = await parser.PopIf(TokenType.Terminator);

        NamespaceDeclarationNode namespaceStatement = new(token, expression, terminator, parser.CurrentFileNode.FilePath);

        return namespaceStatement;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
