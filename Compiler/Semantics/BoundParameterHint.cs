using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Captures the compiler-established parameter mapping for one invocation argument.
/// </summary>
/// <param name="ArgumentSpan">The argument expression range in its authoritative snapshot.</param>
/// <param name="Callable">The uniquely selected callable.</param>
/// <param name="Parameter">The declared parameter receiving the argument.</param>
/// <param name="IsNamedArgument">Whether source syntax already labels the argument.</param>
/// <param name="IsDirectIdentifierMatch">Whether a direct identifier matches the parameter name exactly.</param>
internal sealed record BoundParameterHint(
    SourceSpan ArgumentSpan,
    PackageCallable Callable,
    BoundParameter Parameter,
    bool IsNamedArgument,
    bool IsDirectIdentifierMatch);
