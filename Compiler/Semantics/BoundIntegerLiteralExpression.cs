namespace Sushi.Semantics;

/// <summary>
/// Represents an integer constant after contextual typing has established its concrete type.
/// </summary>
/// <param name="Value">
/// The concrete int32 value.
/// </param>
public sealed record BoundIntegerLiteralExpression(int Value) : BoundExpression(BoundIntegerType.Int32);