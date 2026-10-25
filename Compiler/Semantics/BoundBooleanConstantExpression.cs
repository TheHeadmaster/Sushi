namespace Sushi.Semantics;

/// <summary>
/// Represents a Boolean expression whose result is known during compilation.
/// </summary>
/// <param name="Value">
/// The constant Boolean value produced by the expression.
/// </param>
public sealed record BoundBooleanConstantExpression(bool Value) : BoundExpression(BoundType.Bool);