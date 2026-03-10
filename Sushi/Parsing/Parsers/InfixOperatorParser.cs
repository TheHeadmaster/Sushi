using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class InfixOperatorParser(BindingPower power) : IInfixParser
{
    public async Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] ExpressionNode left, [NotNull] Token token)
    {
        ExpressionNode right = await parser.ParseExpression(power);
        return new BinaryExpressionNode(token, left, right);
    }

    public BindingPower Power() => power;
}