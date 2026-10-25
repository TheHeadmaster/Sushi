namespace Sushi.Semantics;

/// <summary>
/// Identifies a built-in Boolean binary operation after semantic resolution.
/// Logical operations short-circuit; eager operations evaluate both operands.
/// </summary>
public enum BooleanBinaryOperator
{
    LogicalAnd,
    LogicalOr,
    EagerAnd,
    EagerOr,
    EagerXor
}