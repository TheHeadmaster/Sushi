namespace Sushi.Semantics;

/// <summary>
/// Represents semantic information produced by binding one Sushi source file.
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
public sealed record SourceFileBindResult(PackageIdentity? Package, NamespaceIdentity? Namespace, IReadOnlyList<BoundFunction> Functions);