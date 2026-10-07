namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a linear sequence of lowered instructions ending in exactly one control-flow terminator.
/// </summary>
public sealed record LoweredBasicBlock
{
    /// <summary>
    /// Gets the stable name used to identify the block within its function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the ordinary instructions executed before the block terminator.
    /// </summary>
    public IReadOnlyList<LoweredInstruction> Instructions { get; }

    /// <summary>
    /// Gets the control-flow operation that ends the block.
    /// </summary>
    public LoweredTerminator Terminator { get; }

    /// <summary>
    /// Creates a lowered basic block.
    /// </summary>
    /// <param name="name">
    /// The stable block name within the containing function.
    /// </param>
    /// <param name="instructions">
    /// The ordinary instructions executed in source-independent execution order.
    /// </param>
    /// <param name="terminator">
    /// The control-flow operation that ends the block.
    /// </param>
    public LoweredBasicBlock(string name, IReadOnlyList<LoweredInstruction> instructions, LoweredTerminator terminator)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(terminator);

        this.Name = name;
        this.Instructions = [.. instructions];
        this.Terminator = terminator;
    }
}