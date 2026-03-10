using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public sealed class PostfixOperatorParser : IInfixParser
{
    public Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] ExpressionNode left, [NotNull] Token token)
        => Task.FromResult<ExpressionNode>(new UnaryExpressionNode(token, false, left));
    public BindingPower Power() => BindingPower.Postfix;
}
