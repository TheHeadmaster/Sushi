namespace Sushi.Semantics;

/// <summary>
/// Represents a semantically bound expression with a concrete result type.
/// </summary>
/// <param name="Type">
/// The concrete result type established during semantic binding.
/// </param>
public abstract record BoundExpression(BoundType Type);