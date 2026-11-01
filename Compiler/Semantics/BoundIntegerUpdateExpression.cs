namespace Sushi.Semantics;

/// <summary>
/// Updates an initialized int32 local exactly once and produces either its
/// previous value or its checked replacement value.
/// </summary>
public sealed record BoundIntegerUpdateExpression : BoundExpression
{
    /// <summary>
    /// Gets the authoritative writable local being updated.
    /// </summary>
    public BoundLocal Local { get; }

    /// <summary>
    /// Gets whether the update adds one rather than subtracting one.
    /// </summary>
    public bool IsIncrement { get; }

    /// <summary>
    /// Gets whether the expression produces the new value.
    /// A postfix expression instead produces the previous value.
    /// </summary>
    public bool IsPrefix { get; }

    /// <summary>
    /// Creates a typed integer update without duplicating local storage identity.
    /// </summary>
    /// <param name="local">
    /// The initialized writable int32 local.
    /// </param>
    /// <param name="isIncrement">
    /// Whether one is added rather than subtracted.
    /// </param>
    /// <param name="isPrefix">
    /// Whether to produce the new value.
    /// </param>
    public BoundIntegerUpdateExpression(BoundLocal local, bool isIncrement, bool isPrefix) : base(BoundType.Int32)
    {
        ArgumentNullException.ThrowIfNull(local);

        if (local.Type is not BoundType.Int32)
        {
            throw new ArgumentException("The current integer update subset requires an int32 local.", nameof(local));
        }

        this.Local = local;
        this.IsIncrement = isIncrement;
        this.IsPrefix = isPrefix;
    }
}