using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an expression, which can have one or more sub expressions in a tree hierarchy.
/// </summary>
/// <param name="startToken">
/// The token used to mark the start of the node.
/// </param>
public sealed class ExpressionNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The body of the expression. This can be a terminal, such as a constant,
    /// or a nonterminal, such as a binary operator.
    /// </summary>
    public SyntaxNode? Body { get; set; }

    /// <inheritdoc />
    public override async Task<bool> VisitConstant([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        this.Body = new ConstantNode(token);

        return await this.Body.Visit(context);
    }
}
