using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents program-wide graceful termination with an optional int32 status expression.
/// </summary>
public sealed class ExitStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ExitStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.ExitKeyword.Span.Snapshot, this.ExitKeyword.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the reserved keyword that begins program termination.
    /// </summary>
    public SyntaxToken ExitKeyword { get; }

    /// <summary>
    /// Gets the requested application status, or null when graceful status zero is requested.
    /// </summary>
    public ExpressionSyntax? StatusExpression { get; }

    /// <summary>
    /// Gets the semicolon that ends the statement.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates an exit statement whose syntax belongs to a single source snapshot.
    /// </summary>
    /// <param name="exitKeyword">The token that identifies program-wide termination.</param>
    /// <param name="statusExpression">The optional expression that supplies the int32 status.</param>
    /// <param name="semicolonToken">The statement terminator, which may be missing during parser recovery.</param>
    public ExitStatementSyntax(SyntaxToken exitKeyword, ExpressionSyntax? statusExpression, SyntaxToken semicolonToken)
    {
        if (exitKeyword.Type is not SyntaxType.ExitKeyword)
        {
            throw new ArgumentException("Exit statements require an exit keyword token.", nameof(exitKeyword));
        }

        if (semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Exit statements require a semicolon token.", nameof(semicolonToken));
        }

        SourceSnapshot snapshot = exitKeyword.Span.Snapshot;

        if (!ReferenceEquals(semicolonToken.Span.Snapshot, snapshot)
            || (statusExpression is not null && !ReferenceEquals(statusExpression.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All exit-statement syntax must originate from the same source snapshot.");
        }

        if (statusExpression is not null && statusExpression.Span.Start < exitKeyword.Span.End)
        {
            throw new ArgumentException("An exit status expression must follow the exit keyword.", nameof(statusExpression));
        }

        if (semicolonToken.Span.Start < (statusExpression?.Span.End ?? exitKeyword.Span.End))
        {
            throw new ArgumentException("An exit semicolon must follow the status expression.");
        }

        this.ExitKeyword = exitKeyword;
        this.StatusExpression = statusExpression;
        this.SemicolonToken = semicolonToken;
    }
}