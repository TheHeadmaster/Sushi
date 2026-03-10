using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a variable or member assignment.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class AssignmentNode(Token startToken, ReferenceScope scope) : ExpressionableNode(startToken, scope)
{
    /// <summary>
    /// The left hand side of the assignment.
    /// </summary>
    public IdentifierNode? Identifier { get; set; }

    /// <summary>
    /// The right hand side of the assignment.
    /// </summary>
    public ExpressionNode? RightHandSide { get; set; }

    /// <inheritdoc />
    public override SushiType? EvaluateType() => this.Identifier?.EvaluateType();

    /// <inheritdoc />
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
            IdentifierNode identifier = new(token, this.Scope, false);
            if (!await identifier.Visit(context))
            {
                return false;
            }

            this.Identifier = identifier;

            return await this.Visit(context);
        }

        if (this.RightHandSide is null && token.Type is TokenType.AssignmentOperator)
        {
            context.Pop();

            if (context.IsAtEnd())
            {
                context.Errors.Add(new CompilerError(context.EndOfFileToken())
                {
                    ErrorReason = "Unexpected end of file."
                });

                return false;
            }

            ExpressionNode expression = new(token, this.Scope);
            if (!await expression.Visit(context))
            {
                return false;
            }

            this.RightHandSide = expression;

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
