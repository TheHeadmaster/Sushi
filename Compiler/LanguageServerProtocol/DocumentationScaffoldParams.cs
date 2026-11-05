#pragma warning disable CA1056 // DocumentUri is the LSP wire type used by document synchronization.
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Requests compiler-backed documentation metadata for a just-typed triple-slash line.
/// </summary>
[Method("sourceDocumentation/scaffold", Direction.ClientToServer)]
public sealed record DocumentationScaffoldParams : IRequest<DocumentationScaffoldResponse>
{
    /// <summary>
    /// Gets the synchronized source document.
    /// </summary>
    public DocumentUri DocumentUri { get; init; } = null!;

    /// <summary>
    /// Gets the exact editor version that triggered the request.
    /// </summary>
    public int DocumentVersion { get; init; }

    /// <summary>
    /// Gets the zero-based line containing the introducer.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets the zero-based UTF-16 position immediately after the introducer.
    /// </summary>
    public int Character { get; init; }
}