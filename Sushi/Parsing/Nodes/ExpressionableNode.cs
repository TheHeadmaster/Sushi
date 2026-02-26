using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public abstract class ExpressionableNode(Token startToken) : SyntaxNode(startToken)
{
    public abstract TypeNode EvaluateType();
}
