using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Applies the extension's standard workspace configuration notification for parameter hints.
/// </summary>
/// <param name="workspace">The workspace holding the current hint presentation mode.</param>
public sealed class ParameterInlayHintConfigurationHandler(WorkspaceOrchestrator workspace) : DidChangeConfigurationHandlerBase
{
    private readonly WorkspaceOrchestrator workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

    /// <inheritdoc />
    public override async Task<Unit> Handle(DidChangeConfigurationParams request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string? configuredMode = request.Settings?["sushi"]?["parameterInlayHints"]?["mode"]?.ToObject<string>();
        ParameterInlayHintMode? mode = configuredMode switch
        {
            "off" => ParameterInlayHintMode.Off,
            "selective" => ParameterInlayHintMode.Selective,
            "all" => ParameterInlayHintMode.All,
            _ => null
        };

        if (mode is ParameterInlayHintMode selectedMode)
        {
            await this.workspace.SetParameterInlayHintModeAsync(selectedMode, cancellationToken);
        }

        return Unit.Value;
    }
}
