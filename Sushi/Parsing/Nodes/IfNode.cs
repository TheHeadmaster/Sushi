using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class IfNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
{
    public ExpressionNode Expression { get; set; }

    public BlockNode Body { get; set; }

    private readonly ReferenceScope blockScope = new(scope);

    private bool IfConsumed { get; set; }

    private bool ThenConsumed { get; set; }

    /// <inheritdoc />
    public override async Task<bool> Visit([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;
        Token? realToken = context.PeekNextNonWhiteSpaceNonReturnToken(0);

        if (token is null)
        {
            context.Errors.Add(new CompilerError(context.EndOfFileToken())
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        if (this.IfConsumed && token.Type is TokenType.Keyword && token.Value == "if")
        {
            context.Errors.Add(new CompilerError(realToken)
            {
                ErrorReason = "Unexpected if keyword in if statement."
            });

            return false;
        }
        else if (!this.IfConsumed && token.Type is TokenType.Keyword && token.Value == "if")
        {
            this.IfConsumed = true;
            context.Pop();
            return await this.Visit(context);
        }

        if (this.Expression is null)
        {
            ExpressionNode expression = new(token, this.blockScope);

            if (!await expression.Visit(context))
            {
                return false;
            }

            this.Expression = expression;

            return await this.Visit(context);
        }

        if (realToken is null)
        {
            context.Errors.Add(new CompilerError(context.EndOfFileToken())
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        if (this.ThenConsumed && realToken.Type is TokenType.Keyword && realToken.Value == "then")
        {
            context.Errors.Add(new CompilerError(realToken)
            {
                ErrorReason = "Unexpected then keyword in if statement."
            });

            return false;
        }
        else if (!this.ThenConsumed && realToken.Type is TokenType.Keyword && realToken.Value == "then")
        {
            this.ThenConsumed = true;
            context.PopUntilNonWhiteSpaceNonReturnTokenOrEndOfFile();
            context.Pop();
            return await this.Visit(context);
        }

        if (this.Body is null)
        {
            BlockNode body = new(token, this.blockScope);

            if (!await body.Visit(context))
            {
                return false;
            }

            this.Body = body;

            return await this.Visit(context);
        }

        return true;
    }
}
