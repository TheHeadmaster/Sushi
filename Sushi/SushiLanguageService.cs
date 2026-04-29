using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Sushi;

/// <summary>
/// Service that handles the coordination of lexing, parsing, updating, and compiling of Sushi codebases.
/// </summary>
public sealed class SushiLanguageService
{
    /// <summary>
    /// The workspace folders from the client. Contains the sushi project files when running in LSP mode.
    /// </summary>
    private readonly List<WorkspaceFolder> folders = [];

    /// <summary>
    /// Initializes the language service with the information about the currently open workspace. Only used in LSP mode.
    /// </summary>
    /// <param name="workspaceFolders">
    /// The folders that are a part of the workspace.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task InitializeWorkspace([NotNull] List<WorkspaceFolder> workspaceFolders) => await this.ReplaceWorkspaceFolders(workspaceFolders);

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

    /// <summary>
    /// Updates the workspace folders with the current changeset from the changed workspace folders.
    /// </summary>
    /// <param name="facade">
    /// The language server facade.
    /// </param>
    /// <param name="addedFolders">
    /// A list of folders that were added.
    /// </param>
    /// <param name="removedFolders">
    /// A list of folders that were removed.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task UpdateWorkspaceFolders([NotNull] ILanguageServerFacade facade, [NotNull] List<WorkspaceFolder> addedFolders, [NotNull] List<WorkspaceFolder> removedFolders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (WorkspaceFolder addedFolder in addedFolders)
        {
            this.folders.Add(addedFolder);
        }

        foreach (WorkspaceFolder removedFolder in removedFolders)
        {
            this.folders.Remove(removedFolder);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Replaces the current workspace folders with an entirely new set.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private Task ReplaceWorkspaceFolders([NotNull] IEnumerable<WorkspaceFolder> workspaceFolders)
    {
        this.folders.Clear();
        this.folders.AddRange(workspaceFolders);

        return Task.CompletedTask;
    }
}