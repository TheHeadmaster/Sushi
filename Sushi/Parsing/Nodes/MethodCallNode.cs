using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class MethodCallNode(Token startToken, ReferenceScope scope) : ExpressionableNode(startToken, scope)
{
    public IdentifierNode Identifier { get; set; }

    public ArgumentListNode Arguments { get; set; }

    public override SushiType? EvaluateType() => this.Identifier?.EvaluateType();

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

        if (this.Identifier is null)
        {
            IdentifierNode identifier = new(token, this.Scope, true);
            
            if (!await identifier.Visit(context))
            {
                return false;
            }

            this.Identifier = identifier;

            return await this.Visit(context);
        }

        if (this.Arguments is null)
        {
            ArgumentListNode arguments = new(token, this.Scope);

            if (!await arguments.Visit(context))
            {
                return false;
            }

            this.Arguments = arguments;

            return await this.Visit(context);
        }

        return true;
    }
}
