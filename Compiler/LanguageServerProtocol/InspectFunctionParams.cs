#pragma warning disable CA1054, CA1056 // Protocol DTO URI fields are wire-format strings, validated at the request boundary.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests a representation of the function at a UTF-16 editor position without retransmitting source text.
/// </summary>
[Method("sourceInspection/inspectFunction", Direction.ClientToServer)]
public sealed record InspectFunctionParams : IRequest<InspectFunctionResponse>
{
    /// <summary>
    /// Gets the selected local source URI.
    /// </summary>
    public string SourceUri { get; init; } = "";

    /// <summary>
    /// Gets the editor version expected in the server workspace.
    /// </summary>
    public int? SourceVersion { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 cursor position.
    /// </summary>
    public Position Position { get; init; } = new();

    /// <summary>
    /// Gets structuredIR or loweredIR. Other representations are not supported by this request.
    /// </summary>
    public string Stage { get; init; } = "";

    /// <summary>
    /// Gets an optional explicit local project URI to disambiguate membership.
    /// </summary>
    public string? ProjectUri { get; init; }
}

/// <summary>
/// Releases a closed or abandoned virtual document's session-owned metadata.
/// </summary>
[Method("sourceInspection/release", Direction.ClientToServer)]
public sealed record ReleaseInspectionParams : IRequest
{
    /// <summary>
    /// Gets the opaque artifact identifier returned by inspection.
    /// </summary>
    public string ArtifactId { get; init; } = "";
}