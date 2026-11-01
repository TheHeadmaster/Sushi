namespace Sushi.Semantics;

/// <summary>
/// Represents a statically validated transfer to an enclosing loop.
/// Repetition depth counts loops only, not intervening conditionals or blocks.
/// </summary>
public sealed record BoundLoopTransferStatement : BoundStatement
{
    /// <summary>
    /// Gets whether the selected loop is exited.
    /// False indicates continuation of the selected loop.
    /// </summary>
    public bool IsBreak { get; }

    /// <summary>
    /// Gets the number of enclosing loop boundaries to traverse.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Creates a transfer with a valid positive loop depth.
    /// </summary>
    /// <param name="isBreak">
    /// True for break, false for continue.
    /// </param>
    /// <param name="depth">
    /// The one-based enclosing loop depth.
    /// </param>
    public BoundLoopTransferStatement(bool isBreak, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        this.IsBreak = isBreak;
        this.Depth = depth;
    }
}