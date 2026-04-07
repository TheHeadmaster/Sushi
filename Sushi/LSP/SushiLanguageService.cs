using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Tokenization;
using Builder = System.Collections.Immutable.ImmutableArray<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>.Builder;

namespace Sushi.LSP;

public sealed class SushiLanguageService
{
    private readonly Lexer lexer = new();

    private readonly List<WorkspaceFolder> folders = [];

    private readonly List<TokenFile> tokenFiles = [];

    private Task<List<CompilerMessage>> GetMessages() => Task.FromResult<List<CompilerMessage>>([.. this.tokenFiles.SelectMany(x => x.Messages)]);

    public async Task UpdateWorkspaceFolders(ILanguageServerFacade facade, [NotNull] List<WorkspaceFolder> addedFolders, [NotNull] List<WorkspaceFolder> removedFolders)
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

        await this.UpdateTokenFiles();

        await this.PublishDiagnosticsForAllDocuments(facade, null);

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

    public async Task UpdateSource([NotNull] string sourceFilePath)
    {
        TokenFile file = await Lexer.LexFile(sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => Uri.Compare(new Uri(x.FilePath), new Uri(sourceFilePath), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

        if (existingIndex != -1)
        {

            this.tokenFiles[existingIndex] = file;
        }
    }

    public async Task UpdateSourceText([NotNull] string text, string sourceFilePath)
    {
        TokenFile file = await Lexer.LexStringAsFileText(text, sourceFilePath);

        int existingIndex = this.tokenFiles.FindIndex(x => Uri.Compare(new Uri(x.FilePath), new Uri(sourceFilePath), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

        if (existingIndex != -1)
        {
            this.tokenFiles[existingIndex] = file;
        }
    }

    public async Task Initialize(List<WorkspaceFolder> workspaceFolders)
    {
        await this.UpdateWorkspaceFolders(workspaceFolders);
        await this.UpdateTokenFiles();
    }

    private async Task UpdateWorkspaceFolders(List<WorkspaceFolder> workspaceFolders)
    {
        this.folders.Clear();
        this.folders.AddRange(workspaceFolders);
    }

    private async Task UpdateTokenFiles()
    {
        this.tokenFiles.Clear();

        foreach (WorkspaceFolder folder in this.folders)
        {
            this.tokenFiles.AddRange(await this.lexer.LexFiles(folder.Uri.GetFileSystemPath()));
        }
    }

    public async Task UpdateDocument([NotNull] ILanguageServerFacade facade, [NotNull] DocumentUri textDocumentUri, int? version, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            await this.UpdateSourceText(text, textDocumentUri.ToUri().AbsolutePath);
        }
        else
        {
            await this.UpdateSource(textDocumentUri.ToUri().AbsolutePath);
        }

        await this.PublishDiagnosticsForAllDocuments(facade, version);
    }

    private async Task PublishDiagnosticsForAllDocuments([NotNull] ILanguageServerFacade facade, int? version)
    {
        List<CompilerMessage> messages = await this.GetMessages();

        List<DocumentUri> uris = [.. this.tokenFiles.Select(x => DocumentUri.FromFileSystemPath(x.FilePath))];

        foreach (IGrouping<string, CompilerMessage> group in messages.GroupBy(x => x.FilePath))
        {
            int existingIndex = uris.FindIndex(x => Uri.Compare(x.ToUri(), new Uri(group.Key), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0);

            if (existingIndex != -1)
            {
                uris.RemoveAt(existingIndex);
            }

            DocumentUri document = DocumentUri.FromFileSystemPath(group.Key);
            await this.PublishDiagnosticsForDocument(facade, version, [.. group], document);
        }

        foreach (DocumentUri uri in uris)
        {
            await this.PublishDiagnosticsForDocument(facade, version, [], uri);
        }
    }

    private async Task PublishDiagnosticsForDocument([NotNull] ILanguageServerFacade facade, int? version, List<CompilerMessage> messages, DocumentUri document)
    {
        Builder diagnostics = ImmutableArray<Diagnostic>.Empty.ToBuilder();

        foreach (CompilerMessage message in messages)
        {
            diagnostics.Add(await message.ToDiagnostic());
        }

        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams()
        {
            Diagnostics = new Container<Diagnostic>(diagnostics.ToArray()),
            Uri = document,
            Version = version
        });
    }
}
