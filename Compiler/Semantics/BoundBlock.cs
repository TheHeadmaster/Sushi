namespace Sushi.Semantics;

/// <summary>
/// Represents the semantically bound body of a function.
/// </summary>
/// <param name="Statements">
/// The statements in source order.
/// </param>
public sealed record BoundBlock(IReadOnlyList<BoundStatement> Statements);