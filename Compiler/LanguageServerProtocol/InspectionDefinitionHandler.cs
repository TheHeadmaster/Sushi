using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using Sushi.Inspection;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves generated IR definitions against retained formatter metadata and compilation snapshots.
/// </summary>
[Parallel]
public sealed class InspectionDefinitionHandler(CompilerInspectionService inspection, InspectionArtifactStore artifacts)
    : IJsonRpcRequestHandler<InspectionDefinitionParams, InspectionDefinitionResponse>
{
    /// <summary>
    /// Resolves a local occurrence or lazily generates the referenced function at the original stage.
    /// </summary>
    /// <param name="request">The exact retained artifact identity and generated-text cursor position.</param>
    /// <param name="cancellationToken">Cancels resolution and lazy target generation.</param>
    /// <returns>A semantic destination, an ordinary no-definition result, or a safe failure explanation.</returns>
    public async Task<InspectionDefinitionResponse> Handle(InspectionDefinitionParams request, CancellationToken cancellationToken)
    {
        try
        {
            return await this.HandleCore(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Protocol boundary hides internal details while writing the exception to server logs.
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected inspection definition resolution failure.");
            return new InspectionDefinitionResponse { Error = "Internal definition navigation failure. See the Sushi language-server logs for details." };
        }
#pragma warning restore CA1031
    }

    private async Task<InspectionDefinitionResponse> HandleCore(InspectionDefinitionParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.DocumentUri is null || request.Position is null || string.IsNullOrEmpty(request.ArtifactId))
        {
            return new InspectionDefinitionResponse { Error = "The inspection document identity or cursor position is invalid." };
        }
        Uri requestedUri = request.DocumentUri.ToUri();
        InspectionArtifact? originArtifact = artifacts.Find(request.ArtifactId, requestedUri.AbsoluteUri);
        if (originArtifact is null || originArtifact.Result.Document is null)
        {
            return new InspectionDefinitionResponse { Error = "This inspection document is no longer available. Reopen the representation and try again." };
        }

        int? offset = InspectionTextCoordinates.GetOffset(originArtifact.Result.Document.Text, request.Position.Line, request.Position.Character);
        if (offset is null)
        {
            return new InspectionDefinitionResponse();
        }

        InspectionSymbolOccurrence? selectedOccurrence = InspectionDefinitionResolver.FindOccurrence(originArtifact.Result.Document, offset.Value);
        if (selectedOccurrence is not InspectionSymbolOccurrence origin)
        {
            return new InspectionDefinitionResponse();
        }

        InspectionArtifact targetArtifact = originArtifact;
        InspectionSymbolOccurrence? targetDefinition = null;
        bool generated = false;
        bool isCrossFunction = !StringComparer.Ordinal.Equals(origin.Identity.FunctionSymbol, originArtifact.Result.FunctionSymbol);
        if (isCrossFunction)
        {
            if (origin.Identity.Kind is not (InspectionSymbolKind.Function or InspectionSymbolKind.Parameter)
                || !originArtifact.Result.Context.Functions.ContainsKey(origin.Identity.FunctionSymbol))
            {
                return new InspectionDefinitionResponse();
            }

            targetArtifact = artifacts.FindCompatible(originArtifact.Result.Context, originArtifact.Result.Stage, origin.Identity.FunctionSymbol)!;
            if (targetArtifact is null)
            {
                string? registeredId = null;
                try
                {
                    CompilerInspectionResult result = await inspection.InspectFunctionAsync(originArtifact.Result.Context,
                        origin.Identity.FunctionSymbol, originArtifact.Result.Stage, cancellationToken);
                    if (!result.Succeeded || result.Document is null)
                    {
                        string? diagnostic = result.Diagnostics.FirstOrDefault()?.Message;
                        return new InspectionDefinitionResponse
                        {
                            Error = diagnostic is null
                                ? "The referenced function could not be represented at the originating inspection stage."
                                : $"The referenced function could not be represented at the originating inspection stage: {diagnostic}"
                        };
                    }

                    targetArtifact = artifacts.AddOrFindCompatible(result, cancellationToken, out generated);
                    registeredId = generated ? targetArtifact.Id : null;
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    if (registeredId is not null)
                    {
                        artifacts.Release(registeredId);
                    }

                    throw;
                }
                catch (InspectionInputException exception)
                {
                    return new InspectionDefinitionResponse { Error = exception.Message };
                }
#pragma warning disable CA1031 // Protocol requests return a safe message and keep implementation details in server logs.
                catch (Exception exception)
                {
                    Log.Error(exception, "Unable to generate a same-stage inspection navigation target.");
                    return new InspectionDefinitionResponse { Error = "The referenced function could not be generated from the retained compilation snapshot." };
                }
#pragma warning restore CA1031
            }

            InspectionSymbolIdentity expected = origin.Identity.Kind == InspectionSymbolKind.Function
                ? new InspectionSymbolIdentity(InspectionSymbolKind.Function, origin.Identity.FunctionSymbol)
                : origin.Identity;
            InspectionSymbolOccurrence? foundDefinition = targetArtifact.Result.Document!.Occurrences
                .FirstOrDefault(item => item.IsDefinition && item.Identity == expected);
            if (foundDefinition is not InspectionSymbolOccurrence actualDefinition || !actualDefinition.IsDefinition
                || actualDefinition.Identity != expected)
            {
                if (generated)
                {
                    artifacts.Release(targetArtifact.Id);
                }

                return new InspectionDefinitionResponse { Error = "The target representation does not contain the requested semantic definition." };
            }

            targetDefinition = actualDefinition;
        }
        else
        {
            targetDefinition = InspectionDefinitionResolver.Resolve(originArtifact.Result.Document, offset.Value)?.Definition;
            if (targetDefinition is null)
            {
                return new InspectionDefinitionResponse();
            }
        }

        (int Line, int Character)? start = InspectionTextCoordinates.GetPosition(targetArtifact.Result.Document!.Text, targetDefinition.Value.Start);
        (int Line, int Character)? end = InspectionTextCoordinates.GetPosition(targetArtifact.Result.Document.Text, targetDefinition.Value.End);
        (int Line, int Character)? originStart = InspectionTextCoordinates.GetPosition(originArtifact.Result.Document.Text, origin.Start);
        (int Line, int Character)? originEnd = InspectionTextCoordinates.GetPosition(originArtifact.Result.Document.Text, origin.End);
        if (start is null || end is null || originStart is null || originEnd is null)
        {
            if (generated)
            {
                artifacts.Release(targetArtifact.Id);
            }

            return new InspectionDefinitionResponse { Error = "The inspection metadata contains an invalid generated-text range." };
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            if (generated)
            {
                artifacts.Release(targetArtifact.Id);
            }

            throw;
        }
        
        return new InspectionDefinitionResponse
        {
            Found = true,
            TargetDocumentUri = targetArtifact.DocumentUri,
            TargetArtifactId = targetArtifact.Id,
            Stage = targetArtifact.Result.Stage == InspectionStage.StructuredIR ? "structuredIR" : "loweredIR",
            TargetRange = ToRange(start.Value, end.Value),
            OriginRange = ToRange(originStart.Value, originEnd.Value),
            Generated = generated,
            Text = targetArtifact.Result.Document!.Text,
            FunctionLabel = targetArtifact.Result.FunctionName ?? targetArtifact.Result.FunctionSymbol
        };
    }

    private static InspectionProtocolRange ToRange((int Line, int Character) start, (int Line, int Character) end)
        => new(new InspectionProtocolPosition(start.Line, start.Character), new InspectionProtocolPosition(end.Line, end.Character));
}