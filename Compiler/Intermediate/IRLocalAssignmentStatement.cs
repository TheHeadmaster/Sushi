namespace Sushi.Intermediate;

/// <summary>
/// Assigns a newly evaluated value to a previously initialized function-local
/// storage location. The statement produces no value.
/// </summary>
public sealed record IRLocalAssignmentStatement : IRStatement
{
    /// <summary>
    /// Gets the function-unique local storage index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the required storage value type.
    /// </summary>
    public IRType Type { get; }

    /// <summary>
    /// Gets the expression evaluated before the storage location is overwritten.
    /// </summary>
    public IRExpression Value { get; }

    /// <summary>
    /// Creates a typed local assignment.
    /// </summary>
    /// <param name="index">
    /// The existing local declaration index.
    /// </param>
    /// <param name="type">
    /// The local's concrete value type.
    /// </param>
    /// <param name="value">
    /// The replacement value expression.
    /// </param>
    public IRLocalAssignmentStatement(int index, IRType type, IRExpression value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(value);

        if (!Enum.IsDefined(type) || type is IRType.Void || value.Type != type)
        {
            throw new ArgumentException("A local assignment requires a matching, non-void value.", nameof(value));
        }

        this.Index = index;
        this.Type = type;
        this.Value = value;
    }
}