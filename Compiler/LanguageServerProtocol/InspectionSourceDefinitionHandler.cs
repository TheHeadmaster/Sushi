using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using Sushi.Inspection;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves function, parameter, and local source tokens from an inspection's retained snapshots.
/// </summary>
[Parallel]
public sealed class InspectionSourceDefinitionHandler(InspectionArtifactStore artifacts)
    : IJsonRpcRequestHandler<InspectionSourceDefinitionParams, InspectionSourceDefinitionResponse>
{
    /// <summary>
    /// Resolves source provenance without binding or compiling any additional function.
    /// </summary>
    /// <param name="request">
    /// The exact inspection artifact identity and generated-text cursor position.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the metadata lookup.
    /// </param>
    /// <returns>
    /// A source token and captured text, no target, or a safe artifact/coordinate error.
    /// </returns>
    public Task<InspectionSourceDefinitionResponse> Handle(InspectionSourceDefinitionParams request, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(this.HandleCore(request, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Protocol boundaries log internal exceptions and return safe errors.
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected inspection source-definition resolution failure.");
            return Task.FromResult(new InspectionSourceDefinitionResponse
            {
                Error = "Internal source-definition navigation failure. See the Sushi language-server logs for details."
            });
        }
#pragma warning restore CA1031
    }

    private InspectionSourceDefinitionResponse HandleCore(InspectionSourceDefinitionParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.DocumentUri is null || request.Position is null || string.IsNullOrEmpty(request.ArtifactId))
        {
            return new InspectionSourceDefinitionResponse { Error = "The inspection document identity or cursor position is invalid." };
        }

        InspectionArtifact? artifact = artifacts.Find(request.ArtifactId, request.DocumentUri.ToUri().AbsoluteUri);
        if (artifact?.Result.Document is not InspectionDocument document)
        {
            return new InspectionSourceDefinitionResponse { Error = "This inspection document is no longer available. Reopen the representation and try again." };
        }

        int? offset = InspectionTextCoordinates.GetOffset(document.Text, request.Position.Line, request.Position.Character);
        if (offset is null)
        {
            return new InspectionSourceDefinitionResponse();
        }

        InspectionSourceDefinition? definition = InspectionSourceDefinitionResolver.Resolve(document, offset.Value,
            artifact.Result.Context, artifact.Result.LocalDefinitions);
        if (definition is null)
        {
            return new InspectionSourceDefinitionResponse();
        }

        SourceSpan sourceSpan = definition.SourceSpan;
        SourceSnapshot snapshot = sourceSpan.Snapshot;
        if (!artifact.Result.Context.Sources.Any(source => ReferenceEquals(source, snapshot))
            || !snapshot.IsValidUtf8
            || !IsUtf8Boundary(snapshot.Bytes.Span, sourceSpan.Start)
            || !IsUtf8Boundary(snapshot.Bytes.Span, sourceSpan.End))
        {
            return new InspectionSourceDefinitionResponse { Error = "The retained source token cannot be presented faithfully." };
        }

        (int Line, int Character) start = snapshot.GetUtf16Position(sourceSpan.Start);
        (int Line, int Character) end = snapshot.GetUtf16Position(sourceSpan.End);
        (int Line, int Character)? originStart = InspectionTextCoordinates.GetPosition(document.Text, definition.Occurrence.Start);
        (int Line, int Character)? originEnd = InspectionTextCoordinates.GetPosition(document.Text, definition.Occurrence.End);
        if (originStart is null || originEnd is null)
        {
            return new InspectionSourceDefinitionResponse { Error = "The inspection metadata contains an invalid generated-text range." };
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new InspectionSourceDefinitionResponse
        {
            Found = true,
            SourceUri = DocumentUri.FromFileSystemPath(snapshot.Uri.LocalPath).ToString(),
            Text = snapshot.Text,
            SourceVersion = snapshot.Version,
            SourceRange = ToRange(start, end),
            OriginRange = ToRange(originStart.Value, originEnd.Value)
        };
    }

    private static bool IsUtf8Boundary(ReadOnlySpan<byte> bytes, int offset)
        => offset >= 0 && offset <= bytes.Length && (offset == bytes.Length || (bytes[offset] & 0b1100_0000) != 0b1000_0000);

    private static InspectionProtocolRange ToRange((int Line, int Character) start, (int Line, int Character) end)
        => new(new InspectionProtocolPosition(start.Line, start.Character), new InspectionProtocolPosition(end.Line, end.Character));
}
