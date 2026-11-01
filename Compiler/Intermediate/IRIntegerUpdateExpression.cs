namespace Sushi.Intermediate;

/// <summary>
/// Updates a function-local int32 storage slot and produces the previous
/// or replacement value according to prefix/postfix semantics.
/// </summary>
public sealed record IRIntegerUpdateExpression : IRExpression
{
    /// <summary>
    /// Gets the unique destination local storage index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets whether the update adds one rather than subtracting one.
    /// </summary>
    public bool IsIncrement { get; }

    /// <summary>
    /// Gets whether the expression produces the updated value.
    /// </summary>
    public bool IsPrefix { get; }

    /// <summary>
    /// Creates a typed read-modify-write IR expression.
    /// </summary>
    /// <param name="index">
    /// The initialized int32 local's storage index.
    /// </param>
    /// <param name="isIncrement">
    /// Whether to perform checked addition.
    /// </param>
    /// <param name="isPrefix">
    /// Whether to produce the new rather than previous value.
    /// </param>
    public IRIntegerUpdateExpression(int index, bool isIncrement, bool isPrefix) : base(IRType.Int32)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        this.Index = index;
        this.IsIncrement = isIncrement;
        this.IsPrefix = isPrefix;
    }
}