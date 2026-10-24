using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an expression explicitly grouped by parentheses.
/// </summary>
public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ParenthesizedExpression;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.OpenParenthesisToken.Span.Snapshot, this.OpenParenthesisToken.Span.Start, this.CloseParenthesisToken.Span.End);

    /// <summary>
    /// Gets the opening parenthesis, including a recovered token when missing.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the grouped expression.
    /// </summary>
    public ExpressionSyntax Expression { get; }

    /// <summary>
    /// Gets the closing parenthesis, including a recovered token when missing.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Creates a parenthesized expression while preserving explicit source grouping.
    /// </summary>
    /// <param name="openParenthesisToken">The opening parenthesis.</param>
    /// <param name="expression">The grouped expression.</param>
    /// <param name="closeParenthesisToken">The closing parenthesis.</param>
    public ParenthesizedExpressionSyntax(SyntaxToken openParenthesisToken, ExpressionSyntax expression, SyntaxToken closeParenthesisToken)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken)
        {
            throw new ArgumentException("Parenthesized expressions require an opening parenthesis token.", nameof(openParenthesisToken));
        }

        if (closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("Parenthesized expressions require a closing parenthesis token.", nameof(closeParenthesisToken));
        }

        SourceSpan openSpan = openParenthesisToken.Span;
        SourceSpan expressionSpan = expression.Span;
        SourceSpan closeSpan = closeParenthesisToken.Span;

        if (!ReferenceEquals(openSpan.Snapshot, expressionSpan.Snapshot)
            || !ReferenceEquals(openSpan.Snapshot, closeSpan.Snapshot))
        {
            throw new ArgumentException("All parenthesized-expression syntax must originate from the same source snapshot.");
        }

        if (openSpan.End > expressionSpan.Start || expressionSpan.End > closeSpan.Start)
        {
            throw new ArgumentException("Parenthesized-expression components must occur in source order without overlapping.");
        }

        this.OpenParenthesisToken = openParenthesisToken;
        this.Expression = expression;
        this.CloseParenthesisToken = closeParenthesisToken;
    }
}