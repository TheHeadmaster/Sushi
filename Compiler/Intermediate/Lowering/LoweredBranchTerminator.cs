namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Transfers execution unconditionally to another basic block in the same function.
/// </summary>
public sealed record LoweredBranchTerminator : LoweredTerminator
{
    /// <summary>
    /// Gets the name of the destination basic block.
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Creates an unconditional branch to a named basic block.
    /// </summary>
    /// <param name="target">
    /// The destination block within the current function.
    /// </param>
    public LoweredBranchTerminator(string target)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);

        this.Target = target;
    }
}