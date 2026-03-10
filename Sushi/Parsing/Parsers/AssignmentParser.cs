using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class AssignmentParser : IInfixParser
{
    public async Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] ExpressionNode left, [NotNull] Token token)
    {
        ExpressionNode right = await parser.ParseExpression(BindingPower.Primary);

        if (left is not IdentifierNode identifier)
        {
            throw new NotImplementedException();
        }

        return new AssignmentNode(identifier, right);
    }
    public BindingPower Power() => BindingPower.Assignment;
}
