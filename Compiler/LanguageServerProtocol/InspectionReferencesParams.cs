#pragma warning disable CA1056 // DocumentUri is the protocol-native representation for LSP document identities.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests references recorded by one retained generated inspection artifact.
/// </summary>
[Method("sourceInspection/references", Direction.ClientToServer)]
public sealed record InspectionReferencesParams : IRequest<InspectionReferencesResponse>
{
    /// <summary>
    /// Gets the exact generated document URI returned by inspection.
    /// </summary>
    public DocumentUri DocumentUri { get; init; } = null!;

    /// <summary>
    /// Gets the opaque session artifact identifier associated with the URI.
    /// </summary>
    public string ArtifactId { get; init; } = "";

    /// <summary>
    /// Gets the zero-based UTF-16 cursor position in generated text.
    /// </summary>
    public Position Position { get; init; } = new();

    /// <summary>
    /// Gets whether definition occurrences should be included in the returned references.
    /// </summary>
    public bool IncludeDeclaration { get; init; }
}

/// <summary>
/// Returns generated-text ranges associated with one symbol in the requested artifact.
/// </summary>
public sealed record InspectionReferencesResponse
{
    /// <summary>
    /// Gets source-ordered locations in the originating generated document.
    /// </summary>
    public IReadOnlyList<InspectionProtocolRange> Ranges { get; init; } = Array.Empty<InspectionProtocolRange>();

    /// <summary>
    /// Gets a safe explanation when the artifact or its formatter metadata is invalid.
    /// </summary>
    public string? Error { get; init; }
}
