using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class ExpressionStatementNode([NotNull] ExpressionNode expression) : StatementNode
{
    public ExpressionNode Expression { get; set; } = expression;

    public override Token GetStartToken() => this.Expression.GetStartToken();
}
