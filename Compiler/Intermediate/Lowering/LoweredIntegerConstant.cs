namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a concrete int32 constant usable by lowered Sushi IR.
/// </summary>
/// <param name="Value">
/// The constant int32 value.
/// </param>
public sealed record LoweredIntegerConstant(int Value) : LoweredValue(IRType.Int32);