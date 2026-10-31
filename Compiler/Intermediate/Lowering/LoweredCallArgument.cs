namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Carries a previously evaluated argument value to its statically bound
/// destination parameter.
/// </summary>
/// <param name="ParameterIndex">
/// The parameter position receiving the value.
/// </param>
/// <param name="Value">
/// The lowered argument value.
/// </param>
public sealed record LoweredCallArgument(int ParameterIndex, LoweredValue Value);