using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ExpressionStatementNode([NotNull] ExpressionNode expression) : StatementNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override Token GetStartToken() => this.Expression.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        await this.Expression.Verify(context);
    }
}
