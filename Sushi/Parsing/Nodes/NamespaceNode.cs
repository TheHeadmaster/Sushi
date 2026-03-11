using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class NamespaceNode([NotNull] IdentifierNode identifier, [NotNull] ExpressionNode right) : ExpressionNode
{
    public ExpressionNode Right { get; set; } = right;

    public IdentifierNode Identifier { get; set; } = identifier;

    public override Token GetStartToken() => this.Identifier.GetStartToken();
}
