namespace Sushi.Intermediate;

/// <summary>
/// Represents a structured pre-test loop. Its condition is lowered
/// into a reusable control-flow block rather than evaluated once.
/// </summary>
/// <param name="Condition">
/// The typed Boolean condition.
/// </param>
/// <param name="Body">
/// The statements executed for each successful condition test.
/// </param>
public sealed record IRWhileStatement(IRExpression Condition, IRBlock Body) : IRStatement;