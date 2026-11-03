using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Serilog;
using Sushi.Inspection;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves semantic references from the immutable occurrence metadata of one retained inspection artifact.
/// </summary>
[Parallel]
public sealed class InspectionReferencesHandler(InspectionArtifactStore artifacts)
    : IJsonRpcRequestHandler<InspectionReferencesParams, InspectionReferencesResponse>
{
    /// <summary>
    /// Returns only matching ranges recorded in the exact requested artifact.
    /// </summary>
    /// <param name="request">
    /// The virtual document identity, artifact identifier, cursor position, and declaration preference.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels reference resolution before a complete response is returned.
    /// </param>
    /// <returns>
    /// Ordered references, an ordinary empty response, or a safe artifact error.
    /// </returns>
    public Task<InspectionReferencesResponse> Handle(InspectionReferencesParams request, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(this.HandleCore(request, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Protocol boundary logs unexpected details and returns a safe response.
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected inspection reference resolution failure.");
            return Task.FromResult(new InspectionReferencesResponse { Error = "Internal inspection reference failure. See the Sushi language-server logs for details." });
        }
#pragma warning restore CA1031
    }

    private InspectionReferencesResponse HandleCore(InspectionReferencesParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.DocumentUri is null || request.Position is null || string.IsNullOrEmpty(request.ArtifactId))
        {
            return new InspectionReferencesResponse { Error = "The inspection document identity or cursor position is invalid." };
        }

        InspectionArtifact? artifact = artifacts.Find(request.ArtifactId, request.DocumentUri.ToUri().AbsoluteUri);
        if (artifact?.Result.Document is not InspectionDocument document)
        {
            return new InspectionReferencesResponse { Error = "This inspection document is no longer available. Reopen the representation and try again." };
        }

        int? offset = InspectionTextCoordinates.GetOffset(document.Text, request.Position.Line, request.Position.Character);
        if (offset is null)
        {
            return new InspectionReferencesResponse();
        }

        IReadOnlyList<InspectionSymbolOccurrence> occurrences = InspectionReferencesResolver.Resolve(document, offset.Value,
            request.IncludeDeclaration);
        List<InspectionProtocolRange> ranges = new(occurrences.Count);
        foreach (InspectionSymbolOccurrence occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (int Line, int Character)? start = InspectionTextCoordinates.GetPosition(document.Text, occurrence.Start);
            (int Line, int Character)? end = InspectionTextCoordinates.GetPosition(document.Text, occurrence.End);
            if (start is null || end is null)
            {
                return new InspectionReferencesResponse { Error = "The inspection metadata contains an invalid generated-text range." };
            }

            ranges.Add(new InspectionProtocolRange(new InspectionProtocolPosition(start.Value.Line, start.Value.Character),
                new InspectionProtocolPosition(end.Value.Line, end.Value.Character)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new InspectionReferencesResponse { Ranges = Array.AsReadOnly(ranges.ToArray()) };
    }
}
