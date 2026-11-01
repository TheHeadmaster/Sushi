namespace Sushi.Semantics;

/// <summary>
/// Represents a bound three-part loop with a scoped initialization sequence,
/// Boolean condition, ordered iteration updates, and independently bound body.
/// </summary>
/// <param name="Initialization">
/// Operations executed exactly once before the first condition evaluation.
/// </param>
/// <param name="Condition">
/// The condition evaluated before every prospective iteration.
/// </param>
/// <param name="Iteration">
/// Operations executed after ordinary body completion or continuation.
/// </param>
/// <param name="Body">
/// The nested-scope body.
/// </param>
public sealed record BoundForStatement(BoundBlock Initialization, BoundExpression Condition, BoundBlock Iteration, BoundBlock Body) : BoundStatement;