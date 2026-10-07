namespace Sushi.Semantics;

/// <summary>
/// Represents a semantically bound expression with a concrete integer type.
/// </summary>
/// <param name="Type"></param>
public abstract record BoundExpression(BoundIntegerType Type);