namespace Sushi.Semantics;

/// <summary>
/// Establishes a local binding after evaluating its initializer exactly once.
/// </summary>
/// <param name="Local">
/// The declaration identity introduced by this statement.
/// </param>
/// <param name="Initializer">
/// The typed expression supplying the initial value.
/// </param>
public sealed record BoundLocalDeclarationStatement(BoundLocal Local, BoundExpression Initializer) : BoundStatement;