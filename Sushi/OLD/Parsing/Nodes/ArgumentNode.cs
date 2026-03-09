using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class ArgumentNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
{
    public ExpressionNode? Argument { get; set; }

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

        if (this.Argument is null)
        {
            ExpressionNode argument = new(token, this.Scope);

            if (!await argument.Visit(context))
            {
                return false;
            }

            this.Argument = argument;

            return await this.Visit(context);
        }

        return true;
    }
}
