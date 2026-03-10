using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Sushi.Parsing.Nodes;

public class AssignmentNode([NotNull] IdentifierNode identifier, [NotNull] ExpressionNode right) : ExpressionNode
{
    public IdentifierNode Identifier { get; set; } = identifier;

    public ExpressionNode Right { get; set; } = right;
}
