namespace Sushi.Semantics;

/// <summary>
/// Represents a post-test loop with a statically checked body and a Boolean
/// condition evaluated after each iteration that reaches its condition.
/// </summary>
/// <param name="Body">
/// The braced body executed before the first condition evaluation.
/// </param>
/// <param name="Condition">
/// The Boolean expression controlling whether another iteration begins.
/// </param>
public sealed record BoundDoWhileStatement(BoundBlock Body, BoundExpression Condition) : BoundStatement;