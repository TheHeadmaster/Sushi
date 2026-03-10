using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class MethodCallNode([NotNull] ExpressionNode method, [NotNull] List<ExpressionNode> arguments) : ExpressionNode
{
    public ExpressionNode Method { get; set; } = method;

    public List<ExpressionNode> Arguments { get; set; } = arguments;
}
