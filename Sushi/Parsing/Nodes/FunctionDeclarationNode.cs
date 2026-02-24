using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a function declaration.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> that acts as the head of the syntax node.
/// </param>
public sealed class FunctionDeclarationNode(Token startToken) : SyntaxNode(startToken)
{
    public string ReturnType { get; set; } = null!;

    /// <summary>
    /// The name of the function.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The statements inside of the body of the function.
    /// </summary>
    public List<SyntaxNode> Body { get; set; } = [];

    public bool ParametersOpened { get; set; }

    public bool ParametersClosed { get; set; }

    public bool BodyOpened { get; set; }

    /// <inheritdoc />
    public override async Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.ReturnType is not null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected keyword token in function declaration, as return type is already defined."
            });

            return false;
        }

        this.ReturnType = Constants.PrimitiveTypes[token.Value];

        context.Pop();

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitIdentifier([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.Name is not null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected identifier token in function declaration, as name is already defined."
            });

            return false;
        }

        this.Name = token.Value;

        context.Pop();

        if (context.IsAtEnd())
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitOpeningParenthesis([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.ParametersOpened)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected opening parenthesis token in function declaration."
            });

            return false;
        }

        this.ParametersOpened = true;

        context.Pop();

        if (context.IsAtEnd())
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitClosingParenthesis([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.ParametersClosed)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected closing parenthesis token in function declaration."
            });
            return false;
        }

        this.ParametersOpened = true;

        context.Pop();

        if (context.IsAtEnd())
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        return await this.Visit(context);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitOpeningSquiggly([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.BodyOpened)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected opening squiggly token in function declaration."
            });
            return false;
        }

        this.BodyOpened = true;

        context.Pop();
        while (true)
        {
            if (context.IsAtEnd())
            {
                context.Errors.Add(new CompilerError(token)
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
                this.Body.Add(varDeclarationNode);

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
        Token token = context.Peek()!;

        context.Pop();

        return true;
    }
}