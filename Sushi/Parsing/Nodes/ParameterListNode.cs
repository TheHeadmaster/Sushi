using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a parameter list inside of a function declaration.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> that acts as the head of the syntax node.
/// </param>
public sealed class ParameterListNode(Token startToken) : SyntaxNode(startToken)
{
    public List<ParameterNode> Parameters { get; set; } = [];

    private bool IsOpened { get; set; }

    public override async Task<bool> VisitOpeningParenthesis([NotNull] ParsingContext context)
    {
        if (this.IsOpened)
        {
            context.Errors.Add(new CompilerError(context.Peek()!)
            {
                ErrorReason = "Unexpected '(' token in function declaration."
            });

            return false;
        }

        this.IsOpened = true;

        context.Pop();

        return await this.Visit(context);
    }

    public override Task<bool> VisitClosingParenthesis([NotNull] ParsingContext context)
    {
        context.Pop();

        return Task.FromResult(true);
    }
}
