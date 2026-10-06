using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an integer literal expression.
/// </summary>
public sealed class IntegerLiteralExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.IntegerLiteralExpression;

    /// <inheritdoc />
    public override SourceSpan Span => this.LiteralToken.Span;

    /// <summary>
    /// Gets the integer literal token.
    /// </summary>
    public SyntaxToken LiteralToken { get; }

    /// <summary>
    /// Creates an integer literal expression.
    /// </summary>
    /// <param name="literalToken">
    /// The integer literal represented by the expression.
    /// </param>
    public IntegerLiteralExpressionSyntax(SyntaxToken literalToken)
    {
        if (literalToken.Type is not SyntaxType.IntegerLiteralToken)
        {
            throw new ArgumentException("Integer literal expressions require an integer literal token.", nameof(literalToken));
        }

        this.LiteralToken = literalToken;
    }
}