using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Inspection;
using Sushi.Documentation;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves a typed documentation introducer against compiler-parsed source declarations.
/// </summary>
/// <param name="workspace">The synchronized source workspace.</param>
[Parallel]
public sealed class DocumentationScaffoldHandler(WorkspaceOrchestrator workspace)
    : IJsonRpcRequestHandler<DocumentationScaffoldParams, DocumentationScaffoldResponse>
{
    /// <inheritdoc />
    public async Task<DocumentationScaffoldResponse> Handle(DocumentationScaffoldParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        Uri documentUri = request.DocumentUri.ToUri();
        InspectionWorkspaceSnapshot inputs;
        try
        {
            inputs = await workspace.CaptureInspectionInputs(documentUri, request.DocumentVersion, cancellationToken);
        }
        catch (InspectionInputException)
        {
            return new DocumentationScaffoldResponse();
        }

        SourceAnalysisResult analysis = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(
            inputs.Selected, new DiagnosticReporter(), cancellationToken);
        DocumentationScaffold? scaffold = DocumentationScaffoldResolver.Resolve(
            analysis, request.Line, request.Character, cancellationToken);
        if (scaffold is null)
        {
            return new DocumentationScaffoldResponse { DocumentVersion = inputs.Selected.Version };
        }

        InspectionWorkspaceSnapshot current;
        try
        {
            current = await workspace.CaptureInspectionInputs(documentUri, request.DocumentVersion, cancellationToken);
        }
        catch (InspectionInputException)
        {
            return new DocumentationScaffoldResponse();
        }

        if (!ReferenceEquals(inputs.Selected, current.Selected))
        {
            return new DocumentationScaffoldResponse();
        }

        return new DocumentationScaffoldResponse
        {
            Eligible = true,
            DocumentVersion = inputs.Selected.Version,
            ParameterNames = scaffold.ParameterNames,
            HasReturnDescription = scaffold.HasReturnDescription
        };
    }
}