using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a do-while block.
/// </summary>
/// <param name="token">
/// The starting token of the block.
/// </param>
/// <param name="condition">
/// The condition expression of the block.
/// </param>
/// <param name="body">
/// The body of the block.
/// </param>
public sealed class DoWhileNode([NotNull] Token token, ExpressionNode? condition, BlockNode? body) : StatementNode
{
    /// <summary>
    /// The condition expression of the block.
    /// </summary>
    public ExpressionNode? Condition { get; set; } = condition;

    /// <summary>
    /// The body of the block.
    /// </summary>
    public BlockNode? Body { get; set; } = body;

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}
