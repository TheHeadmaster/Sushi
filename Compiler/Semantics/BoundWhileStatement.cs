namespace Sushi.Semantics;

/// <summary>
/// Represents a while loop whose condition is typed as bool and whose
/// body has been statically checked in a nested lexical scope.
/// </summary>
/// <param name="Condition">
/// The Boolean expression evaluated before each iteration.
/// </param>
/// <param name="Body">
/// The loop body, which may execute zero or more times.
/// </param>
public sealed record BoundWhileStatement(BoundExpression Condition, BoundBlock Body) : BoundStatement;