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

        while (true)
        {
            if (context.IsAtEnd())
            {
                context.Errors.Add(new CompilerError(context.EndOfFileToken())
                {
                    ErrorReason = "Unexpected end of file."
                });

                return false;
            }

            Token currentToken = context.Peek()!;

            if (currentToken.Type is TokenType.Whitespace or TokenType.Newline)
            {
                context.Pop();
                continue;
            }

            if (currentToken.Type is not TokenType.ClosingParenthesis)
            {
                ParameterNode parameterNode = new(currentToken);
                this.Parameters.Add(parameterNode);

                bool result = await parameterNode.Visit(context);

                currentToken = context.Peek()!;

                if (!result)
                {
                    return false;
                }

                if (currentToken.Type is TokenType.Comma)
                {
                    await this.Visit(context);
                    continue;
                }

                break;
            }

            break;
        }

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override Task<bool> VisitClosingParenthesis([NotNull] ParsingContext context)
    {
        context.Pop();

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override Task<bool> VisitComma([NotNull] ParsingContext context)
    {
        context.Pop();

        return Task.FromResult(true);
    }
}
