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

        if (this.Assignment is null && token.Type is TokenType.AssignmentOperator)
        {
            context.Pop();

            if (context.IsAtEnd())
            {
                context.Errors.Add(new CompilerError(token)
                {
                    ErrorReason = "Unexpected end of file."
                });

                return false;
            }

            ExpressionNode expression = new(token);
            if (!await expression.Visit(context))
            {
                return false;
            }

            this.Assignment = expression;

            return await this.Visit(context);
        }
        
        if (token.Type is TokenType.Terminator)
        {
            context.Pop();

            return true;
        }

        if (!await base.Visit(context))
        {
            context.Errors.Add(new CompilerError(token)
            {
                ErrorReason = "Unexpected token in variable declaration."
            });

            return false;
        }

        return true;
    }
}
