namespace Sushi.Intermediate;

/// <summary>
/// Represents a concrete int32 constant in structured Sushi IR.
/// </summary>
/// <param name="Value">
/// The constant int32 value.
/// </param>
public sealed record IRIntegerConstant(int Value) : IRExpression(IRType.Int32);