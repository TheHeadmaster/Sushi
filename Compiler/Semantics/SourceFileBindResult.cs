using Sushi.Diagnostics;

namespace Sushi.Semantics;

/// <summary>
/// Represents semantic information and diagnostics produced by binding one Sushi source file.
/// </summary>
/// <param name="Package">
/// The source file's package identity when exactly one structurally complete package declaration exists.
/// </param>
/// <param name="Namespace">
/// The source file's namespace identity when exactly one structurally complete namespace declaration exists.
/// </param>
/// <param name="Functions">
/// The source file's bound function declarations.
/// </param>
/// <param name="Diagnostics">
/// Semantic diagnostics produced while binding the source file.
/// </param>
public sealed record SourceFileBindResult(PackageIdentity? Package, NamespaceIdentity? Namespace, IReadOnlyList<BoundFunction> Functions, IReadOnlyList<SushiDiagnostic> Diagnostics);