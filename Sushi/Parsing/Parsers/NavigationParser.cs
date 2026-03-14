using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles parsing of the navigation operator. 
/// </summary>
public class NavigationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Infix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Dot];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token)
    {
        ExpressionNode? right = await parser.ParseExpression(BindingPower.Postfix);

        if (left is not IdentifierNode identifier)
        {
            throw new NotImplementedException();
        }

        return new NamespaceNode(identifier, right);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Navigation;
}
