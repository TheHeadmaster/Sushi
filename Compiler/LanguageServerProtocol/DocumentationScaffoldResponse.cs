namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Returns declaration metadata for an editor-owned documentation snippet.
/// </summary>
public sealed record DocumentationScaffoldResponse
{
    /// <summary>
    /// Gets whether the typed documentation line can be attached to a complete declaration.
    /// </summary>
    public bool Eligible { get; init; }

    /// <summary>
    /// Gets the exact source version analyzed by the compiler.
    /// </summary>
    public int? DocumentVersion { get; init; }

    /// <summary>
    /// Gets parameter names in declaration order, excluding discards.
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; init; } = [];

    /// <summary>
    /// Gets whether a return description belongs in the scaffold.
    /// </summary>
    public bool HasReturnDescription { get; init; }
}