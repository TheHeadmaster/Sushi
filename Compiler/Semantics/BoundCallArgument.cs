namespace Sushi.Semantics;

/// <summary>
/// Associates an evaluated invocation argument with the callee parameter position
/// selected through positional or named argument binding.
/// </summary>
/// <param name="ParameterIndex">
/// The zero-based destination parameter position in the selected callable.
/// </param>
/// <param name="Expression">
/// The concretely typed expression evaluated at this source argument position.
/// </param>
public sealed record BoundCallArgument(int ParameterIndex, BoundExpression Expression);