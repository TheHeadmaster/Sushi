namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents non-returning program-wide termination with a status value evaluated exactly once.
/// </summary>
public sealed record LoweredExitTerminator : LoweredTerminator
{
    /// <summary>
    /// Gets the int32 status value that was evaluated before termination is committed.
    /// </summary>
    public LoweredValue Status { get; }

    /// <summary>
    /// Creates a program-termination terminator carrying an evaluated int32 status.
    /// </summary>
    /// <param name="status">The evaluated application termination status.</param>
    public LoweredExitTerminator(LoweredValue status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.Type is not IRType.Int32)
        {
            throw new ArgumentException("Program termination status must have type int32.", nameof(status));
        }

        this.Status = status;
    }
}
