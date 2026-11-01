namespace Sushi.Semantics;

/// <summary>
/// Executes an integer update in a statement or for-header position and
/// intentionally ignores the expression's produced value.
/// </summary>
/// <param name="Expression">
/// The validated storage update to execute exactly once.
/// </param>
public sealed record BoundIntegerUpdateStatement(BoundIntegerUpdateExpression Expression) : BoundStatement;