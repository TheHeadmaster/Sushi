namespace Sushi.Intermediate;

/// <summary>
/// Represents a return operation in structured Sushi IR.
/// </summary>
/// <param name="Expression">
/// The value returned by the function.
/// </param>
public sealed record IRReturnStatement(IRExpression Expression) : IRStatement;