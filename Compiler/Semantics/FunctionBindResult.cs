using Sushi.Diagnostics;

namespace Sushi.Semantics;

/// <summary>
/// Contains the supported semantic projection of a function declaration.
/// </summary>
/// <param name="Function">
/// The bound function when the declaration can be represented by the current semantic slice.
/// </param>
public sealed record FunctionBindResult(BoundFunction? Function);