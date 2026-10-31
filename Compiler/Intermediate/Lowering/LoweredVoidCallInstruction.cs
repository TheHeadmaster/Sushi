namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Invokes a resolved package function with no result value, after its arguments
/// have been evaluated in source order.
/// </summary>
public sealed record LoweredVoidCallInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the private symbol of the selected function.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the callee parameter types in declaration order.
    /// </summary>
    public IReadOnlyList<IRType> ParameterTypes { get; }

    /// <summary>
    /// Gets evaluated arguments in original source order.
    /// </summary>
    public IReadOnlyList<LoweredCallArgument> Arguments { get; }

    /// <summary>
    /// Creates a void call instruction while validating its parameter mapping.
    /// </summary>
    /// <param name="symbol">
    /// The selected native symbol.
    /// </param>
    /// <param name="parameterTypes">
    /// The ordered parameter signature.
    /// </param>
    /// <param name="arguments">
    /// The evaluated arguments and their destination positions.
    /// </param>
    public LoweredVoidCallInstruction(string symbol, IReadOnlyList<IRType> parameterTypes, IReadOnlyList<LoweredCallArgument> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        IRType[] types = [.. parameterTypes];
        LoweredCallArgument[] values = [.. arguments];
        bool[] supplied = new bool[types.Length];

        foreach (LoweredCallArgument argument in values)
        {
            if (argument is null
                || argument.ParameterIndex < 0
                || argument.ParameterIndex >= types.Length
                || supplied[argument.ParameterIndex]
                || argument.Value.Type != types[argument.ParameterIndex])
            {
                throw new ArgumentException("Call arguments must uniquely match the selected signature.", nameof(arguments));
            }

            supplied[argument.ParameterIndex] = true;
        }

        if (supplied.Any(value => !value))
        {
            throw new ArgumentException("Every call parameter must receive an argument.", nameof(arguments));
        }

        this.Symbol = symbol;
        this.ParameterTypes = types;
        this.Arguments = values;
    }
}