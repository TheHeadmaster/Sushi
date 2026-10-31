
namespace Sushi.Intermediate;

/// <summary>
/// Represents a return operation in structured Sushi IR.
/// </summary>
/// <param name="Expression">
/// The returned value, or null for a return that produces no result.
/// </param>
public sealed record IRReturnStatement(IRExpression? Expression) : IRStatement;