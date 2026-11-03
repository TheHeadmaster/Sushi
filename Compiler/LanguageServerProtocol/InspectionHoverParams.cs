#pragma warning disable CA1056 // DocumentUri is the protocol-native representation for LSP document identities.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests semantic hover information from one retained generated inspection artifact.
/// </summary>
[Method("sourceInspection/hover", Direction.ClientToServer)]
public sealed record InspectionHoverParams : IRequest<InspectionHoverResponse>
{
    /// <summary>
    /// Gets the exact virtual document URI returned by the inspection request.
    /// </summary>
    public DocumentUri DocumentUri { get; init; } = null!;

    /// <summary>
    /// Gets the opaque artifact identifier associated with that virtual document.
    /// </summary>
    public string ArtifactId { get; init; } = "";

    /// <summary>
    /// Gets the zero-based UTF-16 cursor position in generated text.
    /// </summary>
    public Position Position { get; init; } = new();
}

/// <summary>
/// Returns safe display text and the exact generated range of a semantic hover.
/// </summary>
public sealed record InspectionHoverResponse
{
    /// <summary>
    /// Gets whether a semantic symbol with complete hover metadata was found.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets a safe explanation when the artifact identity is stale or invalid.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets a source-level function signature for function symbols.
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// Gets plain-text semantic details for the hovered symbol.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the exact occurrence range used by the editor hover.
    /// </summary>
    public InspectionProtocolRange? Range { get; init; }
}
