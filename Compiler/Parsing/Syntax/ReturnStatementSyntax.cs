using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a return statement.
/// </summary>
public sealed class ReturnStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ReturnStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.ReturnKeyword.Span.Snapshot, this.ReturnKeyword.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the return keyword.
    /// </summary>
    public SyntaxToken ReturnKeyword { get; }

    /// <summary>
    /// Gets the expression whose value is returned.
    /// </summary>
    public ExpressionSyntax Expression { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a return statement.
    /// </summary>
    /// <param name="returnKeyword">
    /// The return keyword.
    /// </param>
    /// <param name="expression">
    /// The expression whose value is returned.
    /// </param>
    /// <param name="semicolonToken">
    /// Gets the terminating semicolon.
    /// </param>
    public ReturnStatementSyntax(SyntaxToken returnKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (returnKeyword.Type is not SyntaxType.ReturnKeyword)
        {
            throw new ArgumentException("Return statements require a return keyword token.", nameof(returnKeyword));
        }

        if (semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Return statements require a semicolon token.", nameof(semicolonToken));
        }

        SourceSnapshot snapshot = returnKeyword.Span.Snapshot;

        if (!ReferenceEquals(expression.Span.Snapshot, snapshot) || !ReferenceEquals(semicolonToken.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("All return-statement syntax must originate from the same source snapshot.");
        }

        this.ReturnKeyword = returnKeyword;
        this.Expression = expression;
        this.SemicolonToken = semicolonToken;
    }
}