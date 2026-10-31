
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a return statement, with an optional expression when the enclosing callable returns a value.
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
    /// Gets the returned expression, or null for a bare return statement.
    /// </summary>
    public ExpressionSyntax? Expression { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a return statement while preserving whether a result expression was supplied.
    /// </summary>
    /// <param name="returnKeyword">
    /// The keyword beginning the return statement.
    /// </param>
    /// <param name="expression">
    /// The optional result expression.
    /// </param>
    /// <param name="semicolonToken">
    /// The semicolon terminating the statement.
    /// </param>
    public ReturnStatementSyntax(SyntaxToken returnKeyword, ExpressionSyntax? expression, SyntaxToken semicolonToken)
    {
        if (returnKeyword.Type is not SyntaxType.ReturnKeyword)
        {
            throw new ArgumentException("Return statements require a return keyword token.", nameof(returnKeyword));
        }

        if (semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Return statements require a semicolon token.", nameof(semicolonToken));
        }

        SourceSnapshot snapshot = returnKeyword.Span.Snapshot;

        if (!ReferenceEquals(semicolonToken.Span.Snapshot, snapshot)
            || (expression is not null && !ReferenceEquals(expression.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All return-statement syntax must originate from the same source snapshot.");
        }

        if (expression is not null && expression.Span.Start < returnKeyword.Span.End)
        {
            throw new ArgumentException("A return expression must follow the return keyword.", nameof(expression));
        }

        if (semicolonToken.Span.Start < (expression?.Span.End ?? returnKeyword.Span.End))
        {
            throw new ArgumentException("A return semicolon must follow the return expression.");
        }

        this.ReturnKeyword = returnKeyword;
        this.Expression = expression;
        this.SemicolonToken = semicolonToken;
    }
}