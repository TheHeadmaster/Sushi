namespace Sushi.Intermediate;

/// <summary>
/// Represents an ordered structured block of Sushi IR statements.
/// </summary>
public sealed record IRBlock
{
    /// <summary>
    /// Gets the statements in execution order.
    /// </summary>
    public IReadOnlyList<IRStatement> Statements { get; }

    /// <summary>
    /// Creates a structured IR block.
    /// </summary>
    /// <param name="statements">
    /// The statements in execution order.
    /// </param>
    public IRBlock(IReadOnlyList<IRStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);

        this.Statements = [.. statements];
    }
}