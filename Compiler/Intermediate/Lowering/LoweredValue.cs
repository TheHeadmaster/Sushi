namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a typed value usable by lowered Sushi IR.
/// </summary>
/// <param name="Type">
/// The concrete type of the value.
/// </param>
public abstract record LoweredValue(IRType Type);