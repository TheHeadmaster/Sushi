using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a node that can be a part of an expression.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public abstract class ExpressionableNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
{
    /// <summary>
    /// Evaluates the type of the node for type inferencing.
    /// </summary>
    /// <returns>
    /// The <see cref="SushiType"/>.
    /// </returns>
    public abstract SushiType? EvaluateType();
}
