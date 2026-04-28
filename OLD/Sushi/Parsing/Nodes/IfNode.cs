using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an if statement block.
/// </summary>
/// <param name="token">
/// The start token of the block.
/// </param>
/// <param name="condition">
/// The condition expression of the block.
/// </param>
/// <param name="body">
/// The body of the block.
/// </param>
/// <param name="elseNode">
/// The else-if or else expression of the block if there is one.
/// </param>
public sealed class IfNode([NotNull] Token token, ExpressionNode? condition, BlockNode? body, IfNode? elseNode) : StatementNode
{
    /// <summary>
    /// The condition expression of the block.
    /// </summary>
    public ExpressionNode? Condition { get; set; } = condition;

    /// <summary>
    /// The body of the block.
    /// </summary>
    public BlockNode? Body { get; set; } = body;

    /// <summary>
    /// The else-if or else expression of the block if there is one.
    /// </summary>
    public IfNode? Else { get; set; } = elseNode;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;
}
