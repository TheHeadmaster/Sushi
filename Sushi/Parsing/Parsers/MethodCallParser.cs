using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class MethodCallParser : IInfixParser
{
    public async Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] ExpressionNode left, [NotNull] Token token)
    {
        List<ExpressionNode> arguments = [];

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
            parser.Pop();
        }

        return new MethodCallNode(left, arguments);
    }

    public BindingPower Power() => BindingPower.Call;
}
