namespace Sushi.Intermediate;

/// <summary>
/// Represents program-wide graceful termination, distinct from returning from the current function.
/// </summary>
public sealed record IRExitStatement : IRStatement
{
    /// <summary>
    /// Gets the int32 status expression, or null for the default successful status.
    /// </summary>
    public IRExpression? StatusExpression { get; }

    /// <summary>
    /// Creates an exit operation whose optional expression has type int32.
    /// </summary>
    /// <param name="statusExpression">The requested application status, or null for status zero.</param>
    public IRExitStatement(IRExpression? statusExpression)
    {
        if (statusExpression is not null && statusExpression.Type is not IRType.Int32)
        {
            throw new ArgumentException("A program exit status expression must have type int32.", nameof(statusExpression));
        }

        this.StatusExpression = statusExpression;
    }
}
