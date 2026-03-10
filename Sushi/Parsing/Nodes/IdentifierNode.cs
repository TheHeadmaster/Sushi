using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class IdentifierNode([NotNull] Token token) : ExpressionNode
{
    public string Name { get; set; } = token.Value;
}
