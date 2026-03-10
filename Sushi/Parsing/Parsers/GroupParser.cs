using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class GroupParser : IPrefixParser
{
    public async Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        ExpressionNode expression = await parser.ParseExpression(BindingPower.Primary);
        
        if (parser.Peek()?.Type is not TokenType.ClosingParenthesis)
        {
            throw new NotImplementedException();
        }

        parser.Pop();

        return expression;
    }

    public BindingPower Power() => BindingPower.Primary;
}
