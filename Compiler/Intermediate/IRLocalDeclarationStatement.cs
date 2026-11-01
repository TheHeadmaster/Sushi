namespace Sushi.Intermediate;

/// <summary>
/// Establishes a function-local single-assignment value after evaluating its initializer.
/// </summary>
public sealed record IRLocalDeclarationStatement : IRStatement
{
    /// <summary>
    /// Gets the unique local declaration index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the local's concrete value type.
    /// </summary>
    public IRType Type { get; }

    /// <summary>
    /// Gets the expression evaluated to initialize the local.
    /// </summary>
    public IRExpression Initializer { get; }

    /// <summary>
    /// Creates a typed local declaration.
    /// </summary>
    /// <param name="index">
    /// The function-unique declaration index.
    /// </param>
    /// <param name="type">
    /// The concrete value type.
    /// </param>
    /// <param name="initializer">
    /// The expression producing the initial value.
    /// </param>
    public IRLocalDeclarationStatement(int index, IRType type, IRExpression initializer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(initializer);

        if (!Enum.IsDefined(type) || type is IRType.Void || initializer.Type != type)
        {
            throw new ArgumentException("A local requires a matching, non-void initializer.", nameof(initializer));
        }

        this.Index = index;
        this.Type = type;
        this.Initializer = initializer;
    }
}