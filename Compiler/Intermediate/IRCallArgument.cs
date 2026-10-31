namespace Sushi.Intermediate;

/// <summary>
/// Preserves a call argument's source evaluation position and resolved destination
/// parameter while translating a bound call into structured IR.
/// </summary>
/// <param name="ParameterIndex">
/// The callee parameter position receiving the evaluated value.
/// </param>
/// <param name="Expression">
/// The typed argument expression.
/// </param>
public sealed record IRCallArgument(int ParameterIndex, IRExpression Expression);