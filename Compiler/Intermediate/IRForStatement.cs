namespace Sushi.Intermediate;

/// <summary>
/// Preserves the four phases of a three-part for loop for control-flow
/// lowering. Continue transfers to iteration, while break transfers to exit.
/// </summary>
/// <param name="Initialization">
/// The ordered operations executed once.
/// </param>
/// <param name="Condition">
/// The typed Boolean condition, including an implicit true for omission.
/// </param>
/// <param name="Iteration">
/// The ordered update operations following each continued iteration.
/// </param>
/// <param name="Body">
/// The statements executed while the condition is true.
/// </param>
public sealed record IRForStatement(IRBlock Initialization, IRExpression Condition, IRBlock Iteration, IRBlock Body) : IRStatement;