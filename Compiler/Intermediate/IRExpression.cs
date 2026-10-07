namespace Sushi.Intermediate;

/// <summary>
/// Represents a value-producing expression in structured Sushi IR.
/// </summary>
/// <param name="Type">
/// The concrete type of the produced value.
/// </param>
public abstract record IRExpression(IRType Type);