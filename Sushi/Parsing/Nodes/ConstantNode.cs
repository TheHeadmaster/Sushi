using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class ConstantNode([NotNull] Token token) : ExpressionNode
{
    public string Value { get; set; } = token.Value;
}