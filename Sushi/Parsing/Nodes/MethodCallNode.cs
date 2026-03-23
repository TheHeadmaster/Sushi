using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class MethodCallNode([NotNull] ExpressionNode method, [NotNull] List<ExpressionNode> arguments) : ExpressionNode
{
    public ExpressionNode Method { get; set; } = method;

    public List<ExpressionNode> Arguments { get; set; } = arguments;

    public override Token GetStartToken() => this.Method.GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        await this.Method.Verify(context);

        foreach (ExpressionNode argument in this.Arguments)
        {
            await argument.Verify(context);
        }
    }
}
