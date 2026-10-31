using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a semicolon-terminated expression in statement position, before semantic eligibility is checked.
/// </summary>
public sealed class ExpressionStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ExpressionStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Expression.Span.Snapshot, this.Expression.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the expression appearing in statement position.
    /// </summary>
    public ExpressionSyntax Expression { get; }

    /// <summary>
    /// Gets the required statement-terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a candidate expression statement without deciding whether its result may be ignored.
    /// </summary>
    /// <param name="expression">
    /// The parsed expression.
    /// </param>
    /// <param name="semicolonToken">
    /// The terminating semicolon, possibly recovered.
    /// </param>
    public ExpressionStatementSyntax(ExpressionSyntax expression, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (semicolonToken.Type is not SyntaxType.SemicolonToken
            || !ReferenceEquals(expression.Span.Snapshot, semicolonToken.Span.Snapshot)
            || semicolonToken.Span.Start < expression.Span.End)
        {
            throw new ArgumentException("An expression statement requires an ordered semicolon in the same source snapshot.", nameof(semicolonToken));
        }

        this.Expression = expression;
        this.SemicolonToken = semicolonToken;
    }
}
