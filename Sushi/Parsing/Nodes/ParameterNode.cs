using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a parameter definition inside of a function declaration.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> that acts as the head of the syntax node.
/// </param>
public sealed class ParameterNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The type of the parameter.
    /// </summary>
    public TypeNode? Type { get; set; }

    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public IdentifierNode? Name { get; set; }
}
