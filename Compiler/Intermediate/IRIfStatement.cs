
namespace Sushi.Intermediate;

/// <summary>
/// Represents structured conditional execution, preserving the Boolean condition
/// and both independently bound statement bodies.
/// </summary>
/// <param name="Condition">
/// The typed Boolean condition.
/// </param>
/// <param name="Then">
/// The branch selected when the condition is true.
/// </param>
/// <param name="Else">
/// The optional branch selected when the condition is false.
/// </param>
public sealed record IRIfStatement(IRExpression Condition, IRBlock Then, IRBlock? Else) : IRStatement;