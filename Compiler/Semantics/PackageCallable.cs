namespace Sushi.Semantics;

/// <summary>
/// Associates a successfully bound package-level function with the private symbol
/// used to emit that function within the current native executable.
/// </summary>
/// <param name="Function">
/// The authoritative declaration and parameter contract used for source-level binding.
/// </param>
/// <param name="Symbol">
/// The unique compiler-generated native symbol, independent of source spelling.
/// </param>
public sealed record PackageCallable(BoundFunction Function, string Symbol)
{
    /// <summary>
    /// Gets the function's semantic source name.
    /// </summary>
    public string Name => this.Function.Name;
}