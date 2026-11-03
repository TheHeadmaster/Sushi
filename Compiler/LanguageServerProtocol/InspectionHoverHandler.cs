using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Serilog;
using Sushi.Inspection;
using Sushi.Intermediate;
using Sushi.Semantics;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Returns semantic hover details using only immutable inspection metadata.
/// </summary>
[Parallel]
public sealed class InspectionHoverHandler(InspectionArtifactStore artifacts)
    : IJsonRpcRequestHandler<InspectionHoverParams, InspectionHoverResponse>
{
    /// <summary>
    /// Resolves a hovered symbol without compiling functions or mutating artifact ownership.
    /// </summary>
    /// <param name="request">
    /// The exact generated artifact identity and cursor position.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the metadata lookup.
    /// </param>
    /// <returns>
    /// Typed hover information, an ordinary no-hover response, or a safe stale-artifact error.
    /// </returns>
    public Task<InspectionHoverResponse> Handle(InspectionHoverParams request, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(this.HandleCore(request, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Protocol boundary logs internal details and returns a safe response.
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected inspection hover resolution failure.");
            return Task.FromResult(new InspectionHoverResponse { Error = "Internal inspection hover failure. See the Sushi language-server logs for details." });
        }
#pragma warning restore CA1031
    }

    private InspectionHoverResponse HandleCore(InspectionHoverParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.DocumentUri is null || request.Position is null || string.IsNullOrEmpty(request.ArtifactId))
        {
            return new InspectionHoverResponse { Error = "The inspection document identity or cursor position is invalid." };
        }

        InspectionArtifact? artifact = this.artifacts.Find(request.ArtifactId, request.DocumentUri.ToUri().AbsoluteUri);
        if (artifact?.Result.Document is not InspectionDocument document)
        {
            return new InspectionHoverResponse { Error = "This inspection document is no longer available. Reopen the representation and try again." };
        }

        int? offset = InspectionTextCoordinates.GetOffset(document.Text, request.Position.Line, request.Position.Character);
        if (offset is null)
        {
            return new InspectionHoverResponse();
        }

        InspectionHoverResult? hover = InspectionHoverResolver.Resolve(document, offset.Value, artifact.Result.Context);
        if (hover is null)
        {
            return new InspectionHoverResponse();
        }

        (int Line, int Character)? start = InspectionTextCoordinates.GetPosition(document.Text, hover.Occurrence.Start);
        (int Line, int Character)? end = InspectionTextCoordinates.GetPosition(document.Text, hover.Occurrence.End);
        if (start is null || end is null)
        {
            return new InspectionHoverResponse { Error = "The inspection metadata contains an invalid generated-text range." };
        }

        cancellationToken.ThrowIfCancellationRequested();
        (string? signature, string details) = GetDisplay(hover, document.Stage);
        return new InspectionHoverResponse
        {
            Found = true,
            Signature = signature,
            Details = details,
            Range = new InspectionProtocolRange(new InspectionProtocolPosition(start.Value.Line, start.Value.Character),
                new InspectionProtocolPosition(end.Value.Line, end.Value.Character))
        };
    }

    private static (string? Signature, string Details) GetDisplay(InspectionHoverResult hover, InspectionStage stage)
    {
        InspectionSymbolIdentity identity = hover.Occurrence.Identity;
        string owner = hover.Callable.Name;
        return identity.Kind switch
        {
            InspectionSymbolKind.Function => (FormatSignature(hover.Callable), $"Generated symbol: {identity.FunctionSymbol}"),
            InspectionSymbolKind.Parameter when hover.Parameter is InspectionParameterSignature parameter =>
                (null, $"Parameter {parameter.Index}: {parameter.Name}\nType: {GetTypeName(parameter.Type)}\nFunction: {owner}"),
            InspectionSymbolKind.Local when hover.Metadata is InspectionValueMetadata local =>
                (null, $"{(stage is InspectionStage.StructuredIR ? "Structured IR local" : "Lowered IR local slot")}: {local.DisplayName}\nType: {GetTypeName(local.Type)}\nFunction: {owner}"),
            InspectionSymbolKind.Temporary when hover.Metadata is InspectionValueMetadata temporary =>
                (null, $"SSA temporary: {temporary.DisplayName}\nType: {GetTypeName(temporary.Type)}\nFunction: {owner}"),
            InspectionSymbolKind.BasicBlock when hover.Metadata is InspectionBasicBlockMetadata block && identity.Index is int index =>
                (null, $"Basic block: {block.Label}\nIndex: {index}\nFunction: {owner}"),
            _ => throw new InvalidOperationException("The hovered semantic symbol has incomplete formatter metadata.")
        };
    }

    private static string FormatSignature(InspectionCallableSignature callable)
    {
        string parameters = string.Join(", ", callable.Parameters.Select(parameter =>
            $"{GetTypeName(parameter.Type)} {parameter.Name}"));
        return $"{GetAccessibilityName(callable.Accessibility)} {GetTypeName(callable.ReturnType)} {callable.Name}({parameters})";
    }

    private static string GetTypeName(BoundType type) => type switch
    {
        BoundType.Int32 => "int32",
        BoundType.Bool => "bool",
        BoundType.Void => "void",
        _ => throw new NotSupportedException($"Bound type \"{type}\" cannot be displayed in inspection hover.")
    };

    private static string GetTypeName(IRType type) => type switch
    {
        IRType.Int32 => "int32",
        IRType.Bool => "bool",
        IRType.Void => "void",
        _ => throw new NotSupportedException($"IR type \"{type}\" cannot be displayed in inspection hover.")
    };

    private static string GetAccessibilityName(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Package => "package",
        Accessibility.Protected => "protected",
        Accessibility.Private => "private",
        _ => throw new NotSupportedException($"Accessibility \"{accessibility}\" cannot be displayed in inspection hover.")
    };
}
