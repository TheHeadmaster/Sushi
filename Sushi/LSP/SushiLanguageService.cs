using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.LSP;

public sealed class SushiLanguageService
{
    private Lexer lexer = new();

    private List<WorkspaceFolder> folders = [];

    List<TokenFile> tokenFiles = [];

    public async Task UpdateProjectFolders([NotNull] List<WorkspaceFolder> addedFolders, [NotNull] List<WorkspaceFolder> removedFolders)
    {
        this.tokenFiles.Clear();

        foreach (WorkspaceFolder addedFolder in addedFolders)
        {
            this.folders.Add(addedFolder);
        }

        foreach (WorkspaceFolder removedFolder in removedFolders)
        {
            this.folders.Remove(removedFolder);
        }

        foreach (WorkspaceFolder folder in this.folders)
        {
            this.tokenFiles.AddRange(await this.lexer.LexFiles(folder.Uri.Path));
        }

        /*
        Parser parser = new();
        List<TokenFile> tokenFiles = await lexer.LexFiles(AppMeta.Options.ProjectPath);

        AbstractSyntaxTree tree = await parser.ParseSource(tokenFiles);

        foreach (CompilerMessage message in tree.Messages.OrderBy(x => x.Type))
        {
            await message.LogMessage();
        }

        List<CompiledFile> compiledFiles = await compiler.Compile(tree, parser.Reference);

        await WriteFilesToDisk(compiledFiles);

        if (!AppMeta.Options.IntermediateOnly)
        {
            await ExeCompiler.Compile("Project");
        }
        */
    }

    public async Task<List<CompilerMessage>> UpdateSource([NotNull] string sourceFilePath)
    {
        TokenFile file = await Lexer.LexFile(sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => x.FilePath == sourceFilePath);

        if (existingIndex != -1)
        {

            this.tokenFiles[existingIndex] = file;
        }

        return file.Messages;
    }

    public async Task<List<CompilerMessage>> UpdateSourceText([NotNull] string text, string sourceFilePath)
    {
        TokenFile file = await Lexer.LexStringAsFileText(text, sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => x.FilePath == sourceFilePath);

        if (existingIndex != -1)
        {

            this.tokenFiles[existingIndex] = file;
        }

        return file.Messages;
    }

    public async Task Initialize(string rootPath)
    {
        this.tokenFiles.Clear();
        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            this.tokenFiles.AddRange(await this.lexer.LexFiles(rootPath));
        }
    }
}
