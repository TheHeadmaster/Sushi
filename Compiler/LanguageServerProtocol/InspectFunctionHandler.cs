using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Inspection;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Bridges captured workspace inputs to compiler inspection and session-owned virtual documents.
/// </summary>
[Parallel]
public sealed class InspectFunctionHandler(WorkspaceOrchestrator workspace, CompilerInspectionService inspection, InspectionArtifactStore artifacts)
    : IJsonRpcRequestHandler<InspectFunctionParams, InspectFunctionResponse>
{
    /// <summary>
    /// Inspects the captured function without holding workspace locks or invoking native compilation.
    /// </summary>
    public async Task<InspectFunctionResponse> Handle(InspectFunctionParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? registered = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectionStage stage = request.Stage switch
            {
                "structuredIR" => InspectionStage.StructuredIR,
                "loweredIR" => InspectionStage.LoweredIR,
                _ => throw new InspectionInputException("Only Structured IR and Lowered IR inspection are supported.")
            };
            if (request.SourceUri is null || request.Position is null)
            {
                throw new InspectionInputException("Inspection requires a source URI and an editor position.");
            }

            Uri sourceUri = request.SourceUri.ToUri();
            InspectionWorkspaceSnapshot inputs = await workspace.CaptureInspectionInputs(sourceUri, request.SourceVersion, cancellationToken);
            int offset;
            try
            {
                offset = inputs.Selected.GetByteOffset(request.Position.Line, request.Position.Character);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new InspectionInputException("The cursor position is not a valid Unicode boundary in the selected source.");
            }

            IReadOnlyList<InspectionProjectResolver.Match> matches = await InspectionProjectResolver.Resolve(inputs, request.ProjectUri, cancellationToken);
            if (matches.Count == 0)
            {
                return new InspectFunctionResponse { Error = "No Sushi project includes this source file. Check the project's source include and exclude patterns." };
            }

            if (matches.Count > 1)
            {
                return new InspectFunctionResponse
                {
                    Error = "This source belongs to multiple Sushi projects. Choose the project to inspect.",
                    Projects = matches.Select(match => new Uri(match.ProjectPath).AbsoluteUri).ToArray()
                };
            }

            InspectionProjectResolver.Match project = matches[0];
            SourceSnapshot[] dependencies = [.. inputs.OpenSources.Where(snapshot => !ReferenceEquals(snapshot, inputs.Selected)
                && project.Sources.Any(path => new Uri(path).IsSamePath(snapshot.Uri)))];
            CompilerInspectionRequest compilerRequest = new(project.ProjectPath, inputs.Selected, offset, stage, dependencies);
            CompilerInspectionResult result;
            try
            {
                result = await inspection.InspectAsync(compilerRequest, cancellationToken);
            }
            catch (ArgumentException exception) when (exception.ParamName == "request")
            {
                throw new InspectionInputException("Select a supported package-level function. Activity lifecycle methods and positions outside functions cannot be inspected yet.");
            }

            if (!result.Succeeded)
            {
                return new InspectFunctionResponse
                {
                    Error = "Inspection could not complete because of source diagnostics.",
                    Diagnostics = result.Diagnostics.Select(ToProtocolDiagnostic).ToArray()
                };
            }

            InspectionArtifact artifact = artifacts.Add(result, cancellationToken);
            registered = artifact.Id;
            cancellationToken.ThrowIfCancellationRequested();
            InspectFunctionResponse response = new()
            {
                Succeeded = true,
                ArtifactId = artifact.Id,
                DocumentUri = artifact.DocumentUri,
                Stage = request.Stage,
                Text = result.Document!.Text,
                FunctionLabel = result.FunctionName ?? result.FunctionSymbol,
                SourceUri = inputs.Selected.Uri.AbsoluteUri,
                SourceVersion = inputs.Selected.Version,
                Diagnostics = result.Diagnostics.Select(ToProtocolDiagnostic).ToArray()
            };
            cancellationToken.ThrowIfCancellationRequested();
            return response;
        }
        catch (OperationCanceledException)
        {
            if (registered is not null)
            {
                artifacts.Release(registered);
            }

            throw;
        }
        catch (InspectionInputException exception)
        {
            return new InspectFunctionResponse { Error = exception.Message };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Unable to read Sushi inspection inputs.");
            return new InspectFunctionResponse { Error = "Unable to read the project or source files. Check that they exist and are accessible." };
        }
#pragma warning disable CA1031 // The protocol boundary logs unexpected failures and returns an explicit failure without leaking stack traces.
        catch (Exception exception)
        {
            if (registered is not null)
            {
                artifacts.Release(registered);
            }

            Log.Error(exception, "Unexpected Sushi inspection failure.");
            return new InspectFunctionResponse { Error = "Internal inspection failure. See the Sushi language-server logs for details." };
        }
#pragma warning restore CA1031
    }

    private static InspectionProtocolDiagnostic ToProtocolDiagnostic(SushiDiagnostic diagnostic)
    {
        (int line, int character) = diagnostic.Span.Snapshot.GetUtf16Position(diagnostic.Span.Start);
        return new InspectionProtocolDiagnostic(diagnostic.Span.Snapshot.Uri.AbsoluteUri, diagnostic.Span.Snapshot.Version,
            diagnostic.DisplayName, diagnostic.Message, line, character);
    }
}

/// <summary>
/// Releases inspection metadata when the editor closes or abandons a virtual document.
/// </summary>
public sealed class ReleaseInspectionHandler(InspectionArtifactStore artifacts) : IJsonRpcNotificationHandler<ReleaseInspectionParams>
{
    /// <summary>
    /// Idempotently releases a session artifact without changing any source document.
    /// </summary>
    public Task<Unit> Handle(ReleaseInspectionParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        artifacts.Release(request.ArtifactId);
        return Task.FromResult(Unit.Value);
    }
}