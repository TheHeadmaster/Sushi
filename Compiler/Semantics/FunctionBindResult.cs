using Sushi.Diagnostics;

namespace Sushi.Semantics;

/// <summary>
/// Contains the supported semantic projection of a function declaration and diagnostics produced while binding it.
/// </summary>
/// <param name="Function">
/// The bound function when the declaration can be represented by the current semantic slice.
/// </param>
/// <param name="Diagnostics">
/// Semantic diagnostics produced while binding the declaration.
/// </param>
public sealed record FunctionBindResult(BoundFunction? Function, IReadOnlyList<SushiDiagnostic> Diagnostics);