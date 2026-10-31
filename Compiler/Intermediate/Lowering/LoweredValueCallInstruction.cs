
namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Invokes a value-producing function and assigns its result to an SSA temporary.
/// Arguments have already been evaluated in source order.
/// </summary>
public sealed record LoweredValueCallInstruction : LoweredInstruction
{
    /// <summary>
    /// Gets the temporary receiving the call result.
    /// </summary>
    public LoweredTemporaryValue Destination { get; }

    /// <summary>
    /// Gets the resolved function symbol.
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the parameter types in declaration order.
    /// </summary>
    public IReadOnlyList<IRType> ParameterTypes { get; }

    /// <summary>
    /// Gets the evaluated arguments in source order.
    /// </summary>
    public IReadOnlyList<LoweredCallArgument> Arguments { get; }

    /// <summary>
    /// Creates a typed, value-producing call instruction.
    /// </summary>
    /// <param name="destination">
    /// The SSA temporary receiving the result.
    /// </param>
    /// <param name="symbol">
    /// The target's private native symbol.
    /// </param>
    /// <param name="parameterTypes">
    /// The selected parameter signature.
    /// </param>
    /// <param name="arguments">
    /// The previously evaluated argument values.
    /// </param>
    public LoweredValueCallInstruction(LoweredTemporaryValue destination, string symbol, IReadOnlyList<IRType> parameterTypes, IReadOnlyList<LoweredCallArgument> arguments)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrEmpty(symbol);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        if (destination.Type is not (IRType.Int32 or IRType.Bool))
        {
            throw new ArgumentException("A value call must produce a supported concrete result type.", nameof(destination));
        }

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
                throw new ArgumentException("Call arguments must uniquely match the selected parameter signature.", nameof(arguments));
            }

            supplied[argument.ParameterIndex] = true;
        }

        if (supplied.Any(value => !value))
        {
            throw new ArgumentException("Every required parameter must receive a value.", nameof(arguments));
        }

        this.Destination = destination;
        this.Symbol = symbol;
        this.ParameterTypes = types;
        this.Arguments = values;
    }
}