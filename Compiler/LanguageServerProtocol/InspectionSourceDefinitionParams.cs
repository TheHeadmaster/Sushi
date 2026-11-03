#pragma warning disable CA1056 // DocumentUri is the protocol-native representation for LSP document identities.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests a source declaration for one semantic occurrence in a retained inspection document.
/// </summary>
[Method("sourceInspection/sourceDefinition", Direction.ClientToServer)]
public sealed record InspectionSourceDefinitionParams : IRequest<InspectionSourceDefinitionResponse>
{
    /// <summary>
    /// Gets the exact generated document URI returned by the inspection request.
    /// </summary>
    public DocumentUri DocumentUri { get; init; } = null!;

    /// <summary>
    /// Gets the opaque artifact identifier associated with that generated document.
    /// </summary>
    public string ArtifactId { get; init; } = "";

    /// <summary>
    /// Gets the zero-based UTF-16 cursor position in generated text.
    /// </summary>
    public Position Position { get; init; } = new();
}

/// <summary>
/// Returns an exact source token and the immutable source snapshot that contains it.
/// </summary>
public sealed record InspectionSourceDefinitionResponse
{
    /// <summary>
    /// Gets whether a function or parameter source declaration was resolved.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets a safe explanation when artifact validation or source coordinates fail.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the normalized URI of the captured source file.
    /// </summary>
    public string? SourceUri { get; init; }

    /// <summary>
    /// Gets the exact text from the captured source snapshot.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the source editor version captured by the inspection, when available.
    /// </summary>
    public int? SourceVersion { get; init; }

    /// <summary>
    /// Gets the half-open declaration-token range in UTF-16 source coordinates.
    /// </summary>
    public InspectionProtocolRange? SourceRange { get; init; }

    /// <summary>
    /// Gets the selected semantic occurrence range in generated-text coordinates.
    /// </summary>
    public InspectionProtocolRange? OriginRange { get; init; }
}
