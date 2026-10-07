namespace Sushi.Semantics;

/// <summary>
/// Represents a package-level function whose currently supported semantics have been bound.
/// </summary>
/// <param name="Accessibility">
/// The function's explicit accessibility.
/// </param>
/// <param name="Name">
/// The semantic function name with source escaping removed.
/// </param>
/// <param name="ReturnType">
/// The concrete integer return type.
/// </param>
/// <param name="Body">
/// The bound function body.
/// </param>
public sealed record BoundFunction(Accessibility Accessibility, string Name, BoundIntegerType ReturnType, BoundBlock Body);