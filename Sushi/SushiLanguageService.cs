using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi codebases.
/// </summary>
public sealed class SushiLanguageService
{
    /// <summary>
    /// Initializes the language service with the information about the currently open workspace. Only used in LSP mode.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The folders that are a part of the workspace.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task InitializeWorkspace(List<WorkspaceFolder> workspaceFolders)
    {

    }

    /// <summary>
    /// Runs a compile job using the current settings. This runs the whole lexing, parsing, and compilation stack (unless IntermediateOnly was passed in).
    /// Only used when not running in LSP mode.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CompileJob()
    {

    }
}