using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an expression as a statement.
/// </summary>
/// <param name="expression"></param>
public sealed class ExpressionStatementNode(ExpressionNode? expression) : StatementNode
{
    /// <summary>
    /// The expression.
    /// </summary>
    public ExpressionNode? Expression { get; set; } = expression;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Expression?.GetStartToken();
}
