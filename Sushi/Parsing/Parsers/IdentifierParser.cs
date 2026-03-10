using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class IdentifierParser : IPrefixParser
{
    public Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<ExpressionNode>(new IdentifierNode(token));
    public BindingPower Power() => BindingPower.Primary;
}
