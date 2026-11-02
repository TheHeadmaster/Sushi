#pragma warning disable CA1054, CA1056 // Protocol DTO URI fields are wire-format strings, validated at the request boundary.
namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Carries only protocol data. Compiler snapshots and semantic objects remain owned by the server session.
/// </summary>
public sealed record InspectFunctionResponse
{
    /// <summary>
    /// Gets whether a completed artifact was produced.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets a concise failure explanation without an internal stack trace.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets matching project URIs when the client must disambiguate membership.
    /// </summary>
    public IReadOnlyList<string> Projects { get; init; } = [];

    /// <summary>
    /// Gets the session artifact key used by future navigation and explicit release.
    /// </summary>
    public string? ArtifactId { get; init; }

    /// <summary>
    /// Gets the unique read-only virtual document URI.
    /// </summary>
    public string? DocumentUri { get; init; }

    /// <summary>
    /// Gets the selected representation's protocol name.
    /// </summary>
    public string? Stage { get; init; }

    /// <summary>
    /// Gets the exact compiler-formatted text on success.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the semantic source function name for display.
    /// </summary>
    public string? FunctionLabel { get; init; }

    /// <summary>
    /// Gets the source URI captured by this artifact.
    /// </summary>
    public string? SourceUri { get; init; }

    /// <summary>
    /// Gets the captured source version, not the editor's later version.
    /// </summary>
    public int? SourceVersion { get; init; }

    /// <summary>
    /// Gets source diagnostics translated to protocol coordinates in their original snapshots.
    /// </summary>
    public IReadOnlyList<InspectionProtocolDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// Describes a compiler diagnostic without serializing internal source objects.
/// </summary>
public sealed record InspectionProtocolDiagnostic(string Uri, int? Version, string Code, string Message, int Line, int Character);