namespace Sushi.Intermediate;

/// <summary>
/// Identifies a typed Boolean operation in structured Sushi IR.
/// Logical operators short-circuit; eager operators always evaluate both operands.
/// </summary>
public enum IRBooleanBinaryOperator
{
    LogicalAnd,
    LogicalOr,
    EagerAnd,
    EagerOr,
    EagerXor
}