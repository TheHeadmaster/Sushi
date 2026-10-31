namespace Sushi.Intermediate;

/// <summary>
/// Represents an invocation producing a concrete value while retaining
/// source-ordered argument evaluation and static parameter binding.
/// </summary>
public sealed record IRCallExpression : IRExpression
{
    /// <summary>
    /// Gets the selected native symbol.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets parameter types in declaration order.
    /// </summary>
    public IReadOnlyList<IRType> ParameterTypes { get; }

    /// <summary>
    /// Gets arguments in their original evaluation order.
    /// </summary>
    public IReadOnlyList<IRCallArgument> Arguments { get; }

    /// <summary>
    /// Creates a value-producing call expression.
    /// </summary>
    /// <param name="symbol">
    /// The resolved native function symbol.
    /// </param>
    /// <param name="type">
    /// The concrete result type.
    /// </param>
    /// <param name="parameterTypes">
    /// The ordered parameter signature.
    /// </param>
    /// <param name="arguments">
    /// The source-ordered arguments and their bound parameter indices.
    /// </param>
    public IRCallExpression(string symbol, IRType type, IReadOnlyList<IRType> parameterTypes, IReadOnlyList<IRCallArgument> arguments) : base(type)
    {
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        if (type is IRType.Void)
        {
            throw new ArgumentException("A value expression cannot return void.", nameof(type));
        }

        this.Symbol = symbol;
        this.ParameterTypes = [.. parameterTypes];
        this.Arguments = [.. arguments];
    }
}