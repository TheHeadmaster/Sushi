namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a concrete Boolean constant usable by lowered Sushi IR.
/// </summary>
/// <param name="Value">
/// The constant Boolean value.
/// </param>
public sealed record LoweredBooleanConstant(bool Value) : LoweredValue(IRType.Bool);