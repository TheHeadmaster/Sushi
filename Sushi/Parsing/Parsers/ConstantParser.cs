using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class ConstantParser : IPrefixParser
{
    public Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<ExpressionNode>(new ConstantNode(token));
    public BindingPower Power() => BindingPower.Primary;
}
