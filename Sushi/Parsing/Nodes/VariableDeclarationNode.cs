using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a variable declaration.
/// </summary>
public sealed class VariableDeclarationNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The type of the variable.
    /// </summary>
    public TypeNode? Type { get; set; }

    /// <summary>
    /// The name of the variable.
    /// </summary>
    public IdentifierNode? Name { get; set; }

    /// <summary>
    /// The right hand side of the variable declaration, if there is one.
    /// </summary>
    public ExpressionNode? Assignment { get; set; }

    /// <inheritdoc />
    public override async Task<bool> Visit([NotNull] ParsingContext context)
    {
        Token? token = context.Peek();

        if (token is null)
        {
            context.Errors.Add(new CompilerError(context.Previous())
            {
                ErrorReason = "Unexpected token in variable declaration."
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
            return await this.Visit(context);
        }

        if (this.Name is null)
        {
            IdentifierNode name = new(token);
            if (!await name.Visit(context))
            {
                return false;
            }
            return await this.Visit(context);
        }

        if (this.Assignment is null)
        {
            ExpressionNode expression = new(token);
            if (!await expression.Visit(context))
            {
                return false;
            }

            return await this.Visit(context);
        }

        if (!await base.Visit(context))
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected token in variable declaration."
            });

            return false;
        }

        context.Pop();

        return true;
    }

    /// <inheritdoc />
    public override async Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        if (Constants.BooleanLiterals.Contains(token.Value))
        {
            if (this.Type is null)
            {
                context.Errors.Add(new CompilerError(token)
                {
                    ErrorReason = "Unexpected boolean literal in variable declaration."
                });
                return false;
            }

            if (this.IsAssigned && this.Assignment is not null)
            {
                ErrorReason
            }
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = "Unexpected keyword in variable declaration."
        });

        return false;
    }

    /// <inheritdoc />
    public override async Task<bool> VisitIdentifier([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.Name is not null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected identifier token in variable declaration, as name is already defined."
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
    public override Task<bool> VisitTerminator([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.Name is null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected terminator token in variable declaration, variable must have an identifier."
            });

            return Task.FromResult(false);
        }

        if (this.IsAssigned && this.Assignment is null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected terminator token after assignment, variable must have an identifier."
            });

            return Task.FromResult(false);
        }

        context.Pop();

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitAssignment([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (this.Name is null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected assignment token in variable declaration, variable must have an identifier."
            });

            return false;
        }

        context.Pop();

        this.IsAssigned = true;

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
    public override async Task<bool> VisitNumberLiteral([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (!this.IsAssigned)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected number literal in variable declaration."
            });

            return false;
        }

        if (this.IsAssigned && this.Assignment is not null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected number literal in variable assignment."
            });

            return false;
        }

        this.Assignment = new NumberLiteralNode(token);

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
    public override async Task<bool> VisitBooleanLiteral([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (!this.IsAssigned)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected boolean literal in variable declaration."
            });

            return false;
        }

        if (this.IsAssigned && this.Assignment is not null)
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected boolean literal in variable assignment."
            });

            return false;
        }

        this.Assignment = new BooleanLiteralNode(token);

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
}
