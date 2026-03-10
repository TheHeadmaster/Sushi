using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public sealed class PrefixOperatorParser() : IPrefixParser
{
    public async Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        ExpressionNode operand = await parser.ParseExpression(BindingPower.Prefix);

        return new UnaryExpressionNode(token, true, operand);
    }

    public BindingPower Power() => BindingPower.Prefix;
}
