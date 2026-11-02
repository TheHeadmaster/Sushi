#pragma warning disable CA1056 // DocumentUri is the protocol-native representation for LSP document identities.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests semantic definition resolution inside one retained generated inspection document.
/// </summary>
[Method("sourceInspection/definition", Direction.ClientToServer)]
public sealed record InspectionDefinitionParams : IRequest<InspectionDefinitionResponse>
{
    /// <summary>Gets the exact generated document URI returned by inspection.</summary>
    public DocumentUri DocumentUri { get; init; } = null!;

    /// <summary>Gets the opaque session artifact identifier returned with the document.</summary>
    public string ArtifactId { get; init; } = "";

    /// <summary>Gets the zero-based UTF-16 cursor position within generated text.</summary>
    public Position Position { get; init; } = new();
}

/// <summary>
/// Describes a generated-text location using LSP line and UTF-16 character coordinates.
/// </summary>
public sealed record InspectionProtocolPosition(int Line, int Character);

/// <summary>
/// Returns the same-stage semantic destination and, when needed, its generated document content.
/// </summary>
public sealed record InspectionDefinitionResponse
{
    /// <summary>Gets whether a semantic definition was resolved.</summary>
    public bool Found { get; init; }

    /// <summary>Gets an understandable failure when the artifact or semantic context is invalid.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the target document URI.</summary>
    public string? TargetDocumentUri { get; init; }

    /// <summary>Gets the retained artifact identifier required to keep the target alive.</summary>
    public string? TargetArtifactId { get; init; }

    /// <summary>Gets the representation stage of the target document.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the target definition range.</summary>
    public InspectionProtocolRange? TargetRange { get; init; }

    /// <summary>Gets the originating reference range when one was selected.</summary>
    public InspectionProtocolRange? OriginRange { get; init; }

    /// <summary>Gets whether resolving the request created a new target artifact.</summary>
    public bool Generated { get; init; }

    /// <summary>Gets exact target text so the client can register content before opening the URI.</summary>
    public string? Text { get; init; }

    /// <summary>Gets a semantic display label for the target function.</summary>
    public string? FunctionLabel { get; init; }
}

/// <summary>
/// Represents a half-open generated-text range in editor coordinates.
/// </summary>
public sealed record InspectionProtocolRange(InspectionProtocolPosition Start, InspectionProtocolPosition End);