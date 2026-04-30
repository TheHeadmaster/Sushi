using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an expression as a statement.
/// </summary>
/// <param name="expression">
/// The expression contained in the statement.
/// </param>
public sealed class ExpressionStatementNode(ExpressionNode? expression) : StatementNode
{
    /// <summary>
    /// The expression.
    /// </summary>
    public ExpressionNode? Expression { get; set; } = expression;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Expression?.GetStartToken();
}