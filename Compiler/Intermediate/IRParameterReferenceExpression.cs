namespace Sushi.Intermediate;

/// <summary>
/// References a function parameter by its declaration index in structured IR.
/// </summary>
public sealed record IRParameterReferenceExpression : IRExpression
{
    /// <summary>
    /// Gets the zero-based parameter position.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Creates a typed IR parameter reference.
    /// </summary>
    /// <param name="index">
    /// The parameter index.
    /// </param>
    /// <param name="type">
    /// The concrete IR type of the referenced value.
    /// </param>
    public IRParameterReferenceExpression(int index, IRType type) : base(type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        this.Index = index;
    }
}