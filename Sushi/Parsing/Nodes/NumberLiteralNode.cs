using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a number literal.
/// </summary>
public sealed class NumberLiteralNode(Token startToken) : SyntaxNode(startToken)
{
    public string Value { get; set; } = startToken.Value;
}
