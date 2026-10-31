namespace Sushi.Semantics;

/// <summary>
/// Represents a resolved, value-less package-function invocation executed as a statement.
/// </summary>
/// <param name="Symbol">
/// The selected compiler-owned native function symbol.
/// </param>
/// <param name="ParameterTypes">
/// The callee's parameter types in declaration order.
/// </param>
/// <param name="Arguments">
/// Explicit arguments in source order, each mapped to its destination parameter.
/// </param>
public sealed record BoundVoidCallStatement(string Symbol, IReadOnlyList<BoundType> ParameterTypes, IReadOnlyList<BoundCallArgument> Arguments) : BoundStatement;