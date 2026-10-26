namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Identifies the value supplied to a PHI instruction by a predecessor basic block.
/// </summary>
public sealed record LoweredPhiIncoming
{
    /// <summary>
    /// Gets the name of the predecessor basic block.
    /// </summary>
    public string Predecessor { get; }

    /// <summary>
    /// Gets the value supplied along that predecessor's outgoing edge.
    /// </summary>
    public LoweredValue Value { get; }

    /// <summary>
    /// Creates a predecessor-qualified PHI incoming value.
    /// </summary>
    /// <param name="predecessor">
    /// The predecessor block's name.
    /// </param>
    /// <param name="value">
    /// The value available along the incoming control-flow edge.
    /// </param>
    public LoweredPhiIncoming(string predecessor, LoweredValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(predecessor);
        ArgumentNullException.ThrowIfNull(value);

        this.Predecessor = predecessor;
        this.Value = value;
    }
}