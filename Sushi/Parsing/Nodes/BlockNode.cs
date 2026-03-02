using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a block body, such as a function block or an if block.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class BlockNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
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
                ErrorReason = "Unexpected '{' token in block body."
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

            if (currentToken.Type is TokenType.ClosingSquiggly)
            {
                break;
            }

            SyntaxNode statement = currentToken.Type is TokenType.Keyword && currentToken.Value == "if"
                ? new IfNode(currentToken, this.Scope)
                : currentToken.Type is not TokenType.Identifier
                    ? new VariableDeclarationNode(currentToken, this.Scope)
                    : new AssignmentNode(currentToken, this.Scope);

            this.Statements.Add(statement);

            bool result = await statement.Visit(context);

            if (!result)
            {
                return false;
            }

            continue;
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
