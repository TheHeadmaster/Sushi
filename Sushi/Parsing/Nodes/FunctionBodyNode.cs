using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a function body.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> that acts as the head of the syntax node.
/// </param>
public sealed class FunctionBodyNode(Token startToken) : SyntaxNode(startToken)
{
    public List<SyntaxNode> Statements { get; set; } = [];

    private bool IsOpened { get; set; }

    /// <inheritdoc />
    public override async Task<bool> VisitOpeningSquiggly([NotNull] ParsingContext context)
    {
        if (this.IsOpened)
        {
            context.Errors.Add(new CompilerError(context.Peek()!)
            {
                ErrorReason = "Unexpected '{' token in function body."
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

            if (currentToken.Type is not TokenType.ClosingSquiggly)
            {
                VariableDeclarationNode varDeclarationNode = new(currentToken);
                this.Statements.Add(varDeclarationNode);

                bool result = await varDeclarationNode.Visit(context);

                if (!result)
                {
                    return false;
                }

                continue;
            }

            break;
        }

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitClosingSquiggly([NotNull] ParsingContext context)
    {
        context.Pop();

        return true;
    }
}
