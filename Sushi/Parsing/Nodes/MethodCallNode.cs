using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a method call.
/// </summary>
/// <param name="method">
/// The expression that resolves to a method being called.
/// </param>
/// <param name="arguments"></param>
public sealed class MethodCallNode(ExpressionNode? method, [NotNull] List<ExpressionNode> arguments) : ExpressionNode
{
    /// <summary>
    /// The expression that resolves to a method being called.
    /// </summary>
    public ExpressionNode? Method { get; set; } = method;

    /// <summary>
    /// The method arguments being passed into the method call.
    /// </summary>
    public List<ExpressionNode> Arguments { get; set; } = arguments;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Method?.GetStartToken();
}
