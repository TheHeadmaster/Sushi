using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an expression, which can have one or more sub expressions in a tree hierarchy.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class ExpressionNode(Token startToken, ReferenceScope scope) : ExpressionableNode(startToken, scope)
{
    /// <summary>
    /// The body of the expression. This can be a terminal, such as a constant,
    /// or a nonterminal, such as a binary operator.
    /// </summary>
    public ExpressionableNode? Body { get; set; }

    /// <inheritdoc />
    public override async Task<bool> VisitConstant([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        this.Body = new ConstantNode(token, this.Scope);

        return await this.Body.Visit(context);
    }

    /// <inheritdoc />
    public override SushiType? EvaluateType() => this.Body!.EvaluateType();
}
