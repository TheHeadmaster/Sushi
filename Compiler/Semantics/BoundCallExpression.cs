namespace Sushi.Semantics;

/// <summary>
/// Represents a value-producing package-function call whose target, return type,
/// and argument-to-parameter mapping have been resolved statically.
/// </summary>
public sealed record BoundCallExpression : BoundExpression
{
    /// <summary>
    /// Gets the private native symbol of the selected callable.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the selected parameter types in declaration order.
    /// </summary>
    public IReadOnlyList<BoundType> ParameterTypes { get; }

    /// <summary>
    /// Gets the arguments in source evaluation order.
    /// </summary>
    public IReadOnlyList<BoundCallArgument> Arguments { get; }

    /// <summary>
    /// Constructs a typed call using statically resolved argument positions.
    /// </summary>
    /// <param name="symbol">
    /// The selected function's compiler-owned symbol.
    /// </param>
    /// <param name="type">
    /// The concrete value type produced by the call.
    /// </param>
    /// <param name="parameterTypes">
    /// Parameter types in declaration order.
    /// </param>
    /// <param name="arguments">
    /// Arguments in source evaluation order.
    /// </param>
    public BoundCallExpression(string symbol, BoundType type, IReadOnlyList<BoundType> parameterTypes, IReadOnlyList<BoundCallArgument> arguments) : base(type)
    {
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        if (type is BoundType.Void)
        {
            throw new ArgumentException("A value-producing call cannot have a void result.", nameof(type));
        }

        this.Symbol = symbol;
        this.ParameterTypes = [.. parameterTypes];
        this.Arguments = [.. arguments];
    }
}