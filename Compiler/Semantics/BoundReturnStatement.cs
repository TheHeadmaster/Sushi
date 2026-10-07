namespace Sushi.Semantics;

/// <summary>
/// Represents a return statement whose expression has been bound against the enclosing function's return contract.
/// </summary>
/// <param name="Expression">
/// The value returned by the statement.
/// </param>
public sealed record BoundReturnStatement(BoundExpression Expression) : BoundStatement;