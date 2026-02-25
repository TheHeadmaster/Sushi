using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    /// <inheritdoc />
    public override async Task<bool> Visit([NotNull] ParsingContext context)
    {
        Token? token = context.Peek();

        if (token is null)
        {
            context.Errors.Add(new CompilerError(context.EndOfFileToken())
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        if (this.Type is null)
        {
            TypeNode type = new(token);
            if (!await type.Visit(context))
            {
                return false;
            }

            this.Type = type;

            return await this.Visit(context);
        }

        if (this.Name is null)
        {
            IdentifierNode name = new(token);
            if (!await name.Visit(context))
            {
                return false;
            }

            this.Name = name;

            return await this.Visit(context);
        }

        return true;
    }
}
