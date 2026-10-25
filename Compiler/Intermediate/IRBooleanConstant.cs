namespace Sushi.Intermediate;

/// <summary>
/// Represents a concrete Boolean constant in structured Sushi IR.
/// </summary>
/// <param name="Value">
/// The constant Boolean value.
/// </param>
public sealed record IRBooleanConstant(bool Value) : IRExpression(IRType.Bool);