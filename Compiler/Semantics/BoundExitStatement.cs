using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Represents non-returning, program-wide graceful termination after an optional status expression.
/// </summary>
public sealed record BoundExitStatement : BoundStatement
{
    /// <summary>
    /// Gets the bound int32 status expression, or null when the default successful status is requested.
    /// </summary>
    public BoundExpression? StatusExpression { get; }

    /// <summary>
    /// Gets the source range of the exit statement in the snapshot that was bound.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// Creates a bound exit statement whose optional expression has the language's int32 status type.
    /// </summary>
    /// <param name="span">The source-backed range of the statement.</param>
    /// <param name="statusExpression">The bound application status, or null for status zero.</param>
    public BoundExitStatement(SourceSpan span, BoundExpression? statusExpression)
    {
        if (statusExpression is not null && statusExpression.Type is not BoundType.Int32)
        {
            throw new ArgumentException("A program exit status expression must have type int32.", nameof(statusExpression));
        }

        this.Span = span;
        this.StatusExpression = statusExpression;
    }
}
