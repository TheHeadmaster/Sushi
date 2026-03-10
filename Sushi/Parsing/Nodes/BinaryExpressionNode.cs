using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class BinaryExpressionNode([NotNull] Token token, [NotNull] ExpressionNode left, [NotNull] ExpressionNode right) : ExpressionNode
{
    public OperatorType Operator { get; set; } = token.Type switch
    {
        TokenType.Plus => OperatorType.Add,
        TokenType.Minus => OperatorType.Subtract,
        TokenType.Asterisk => OperatorType.Multiply,
        TokenType.Slash => OperatorType.Divide,
        _ => throw new NotImplementedException()
    };

    public ExpressionNode Left { get; set; } = left;

    public ExpressionNode Right { get; set; } = right;
}
