using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of namespace declaration statements.
/// </summary>
public class NamespaceDeclarationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Namespace];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Namespace);

        ExpressionNode? expression = await parser.ParseExpression(BindingPower.Primary);

        if (expression is not NamespaceNode namespaceNode)
        {
            throw new NotImplementedException();
        }

        NamespaceDeclarationNode namespaceStatement = new(token, namespaceNode);

        await parser.ExpectAndPop(TokenType.Terminator);

        return namespaceStatement;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
