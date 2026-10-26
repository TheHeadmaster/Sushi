namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Selects a Boolean SSA value according to the basic block from which execution arrived.
/// </summary>
public sealed record LoweredBooleanPhiInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the function-scoped temporary receiving the merged Boolean value.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the predecessor-qualified incoming values.
    /// </summary>
    public IReadOnlyList<LoweredPhiIncoming> Incoming { get; }

    /// <summary>
    /// Creates a Boolean PHI instruction with validated incoming types and predecessor identities.
    /// </summary>
    /// <param name="destination">
    /// The temporary receiving the selected incoming value.
    /// </param>
    /// <param name="incoming">
    /// The incoming values associated with their predecessor blocks.
    /// </param>
    public LoweredBooleanPhiInstruction(LoweredTemporaryValue destination, IReadOnlyList<LoweredPhiIncoming> incoming)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(incoming);

        if (destination.Type is not IRType.Bool)
        {
            throw new ArgumentException("A Boolean PHI instruction requires a Boolean destination.", nameof(destination));
        }

        LoweredPhiIncoming[] incomingArray = [.. incoming];

        if (incomingArray.Length < 2)
        {
            throw new ArgumentException("A Boolean PHI instruction requires at least two incoming edges.", nameof(incoming));
        }

        HashSet<string> predecessors = new(StringComparer.Ordinal);

        foreach (LoweredPhiIncoming item in incomingArray)
        {
            if (item is null || item.Value.Type is not IRType.Bool || !predecessors.Add(item.Predecessor))
            {
                throw new ArgumentException("PHI inputs must be Boolean values from distinct predecessor blocks.", nameof(incoming));
            }
        }

        this.Destination = destination;
        this.Incoming = incomingArray;
    }
}