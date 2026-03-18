using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles parsing method calls.
/// </summary>
public class MethodCallParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Infix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.OpeningParenthesis];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token)
    {
        List<ExpressionNode?> arguments = [];

        if (parser.Peek()?.Type is not TokenType.ClosingParenthesis)
        {
            do
            {
                arguments.Add(await parser.ParseExpression(BindingPower.Primary));
            }
            while (parser.Peek()?.Type is TokenType.Comma);

            if (parser.Peek()?.Type is not TokenType.ClosingParenthesis)
            {
                throw new NotImplementedException();
            }

            parser.Pop();
        }
        else
        {
            if (left is not ICallableNode callable || !callable.ResolvesToIdentifier())
            {
                parser.Messages.Add(new NonInvocableError(left.GetStartToken(), token));
            }

            parser.Pop();
        }

        return new MethodCallNode(left, arguments);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Call;
}
