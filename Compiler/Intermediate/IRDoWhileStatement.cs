namespace Sushi.Intermediate;

/// <summary>
/// Represents a structured post-test loop whose body is entered before
/// evaluating the condition. A continue targeting this loop reaches the
/// trailing condition, and a break bypasses it.
/// </summary>
/// <param name="Body">
/// The structured body executed at least once when reached.
/// </param>
/// <param name="Condition">
/// The Boolean expression controlling subsequent iterations.
/// </param>
public sealed record IRDoWhileStatement(IRBlock Body, IRExpression Condition) : IRStatement;