
namespace Sushi.Semantics;

/// <summary>
/// Represents a typed conditional statement whose branches have been independently checked.
/// </summary>
/// <param name="Condition">
/// The Boolean expression selecting the branch.
/// </param>
/// <param name="Then">
/// Statements executed when the condition evaluates to true.
/// </param>
/// <param name="Else">
/// Optional statements executed when the condition evaluates to false.
/// </param>
public sealed record BoundIfStatement(BoundExpression Condition, BoundBlock Then, BoundBlock? Else) : BoundStatement;