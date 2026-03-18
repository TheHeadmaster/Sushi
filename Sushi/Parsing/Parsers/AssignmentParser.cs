using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of assignments, i.e. the = operator.
/// </summary>
public class AssignmentParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Infix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Assignment];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token)
    {
        ExpressionNode? right = await parser.ParseExpression(BindingPower.Primary);

        if (left is not IdentifierNode identifier)
        {
            throw new NotImplementedException();
        }

        return new AssignmentNode(identifier, right);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Assignment;
}
