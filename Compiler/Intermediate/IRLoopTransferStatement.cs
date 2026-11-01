namespace Sushi.Intermediate;

/// <summary>
/// Transfers control to a selected enclosing loop's exit or continuation
/// target without executing the remaining statements on the current path.
/// </summary>
public sealed record IRLoopTransferStatement : IRStatement
{
    /// <summary>
    /// Gets whether the selected loop is exited.
    /// </summary>
    public bool IsBreak { get; }

    /// <summary>
    /// Gets the one-based number of enclosing loops to traverse.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Creates a structured loop transfer with a positive target depth.
    /// </summary>
    /// <param name="isBreak">
    /// True for break or false for continue.
    /// </param>
    /// <param name="depth">
    /// The one-based enclosing loop depth.
    /// </param>
    public IRLoopTransferStatement(bool isBreak, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        this.IsBreak = isBreak;
        this.Depth = depth;
    }
}