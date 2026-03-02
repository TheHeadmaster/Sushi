using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class ArgumentListNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
{
    public List<ArgumentNode> Arguments { get; set; } = [];

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
                ArgumentNode argumentNode = new(currentToken, this.Scope);
                this.Arguments.Add(argumentNode);

                bool result = await argumentNode.Visit(context);

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
