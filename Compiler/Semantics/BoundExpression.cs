namespace Sushi.Semantics;

/// <summary>
/// Represents a semantically bound expression with a concrete integer type.
/// </summary>
/// <param name="Type">
/// The concrete integer type established during semantic binding.
/// </param>
public abstract record BoundExpression(BoundIntegerType Type);