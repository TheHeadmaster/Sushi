using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles parsing create expressions.
/// </summary>
public class CreateParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Prefix;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Create];

    /// <inheritdoc />
    public async Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? createToken = await parser.ExpectAndPop(TokenType.Create);

        if (createToken is null)
        {
            return null;
        }

        Token? nextToken = await parser.ExpectAndPop(TokenType.Identifier);

        TypeNode? type = nextToken is null ? null : new(nextToken);

        List<ExpressionNode> arguments = [];

        await parser.ExpectAndPop(TokenType.OpeningParenthesis);

        if (parser.Peek()?.Type is not TokenType.ClosingParenthesis)
        {
            do
            {
                ExpressionNode? arg = await parser.ParseExpression(BindingPower.Primary);
                
                if (arg is null)
                {
                    break;
                }

                arguments.Add(arg);
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
            parser.Pop();
        }

        return new CreateNode(token, type, arguments);
    }

    public BindingPower Power(TokenType type) => BindingPower.Create;
}