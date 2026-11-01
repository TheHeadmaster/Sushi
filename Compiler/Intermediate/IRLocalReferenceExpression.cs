namespace Sushi.Intermediate;

/// <summary>
/// References an initialized function-local value using its declaration identity.
/// </summary>
public sealed record IRLocalReferenceExpression : IRExpression
{
    /// <summary>
    /// Gets the function-unique declaration index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a typed local reference independently of the function's parameter ABI.
    /// </summary>
    /// <param name="index">
    /// The local declaration index.
    /// </param>
    /// <param name="type">
    /// The local's concrete value type.
    /// </param>
    public IRLocalReferenceExpression(int index, IRType type) : base(type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!Enum.IsDefined(type) || type is IRType.Void)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
    }
}