
namespace Sushi.Semantics;

/// <summary>
/// Represents a return statement validated against the enclosing callable's return contract.
/// </summary>
/// <param name="Expression">
/// The returned value, or null when control returns without producing a value.
/// </param>
public sealed record BoundReturnStatement(BoundExpression? Expression) : BoundStatement;