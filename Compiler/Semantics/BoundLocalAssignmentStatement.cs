
namespace Sushi.Semantics;

/// <summary>
/// Replaces the value of an initialized local after evaluating a type-compatible
/// right-hand expression. Ordinary assignment produces no result value.
/// </summary>
/// <param name="Local">
/// The writable local identified before evaluating the right-hand expression.
/// </param>
/// <param name="Value">
/// The expression evaluated exactly once to produce the replacement value.
/// </param>
public sealed record BoundLocalAssignmentStatement(BoundLocal Local, BoundExpression Value) : BoundStatement;