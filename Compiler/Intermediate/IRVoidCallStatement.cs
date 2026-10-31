namespace Sushi.Intermediate;

/// <summary>
/// Represents a direct void package-function call with statically resolved
/// argument positions and a compiler-owned callee symbol.
/// </summary>
/// <param name="Symbol">
/// The callee's native symbol.
/// </param>
/// <param name="ParameterTypes">
/// Parameter types in declaration order.
/// </param>
/// <param name="Arguments">
/// Arguments in source evaluation order.
/// </param>
public sealed record IRVoidCallStatement(string Symbol, IReadOnlyList<IRType> ParameterTypes, IReadOnlyList<IRCallArgument> Arguments) : IRStatement;